namespace SlimMessageBus.Host.Mqtt.Test;

using System.Collections.Concurrent;
using System.Diagnostics;

[Trait("Category", "Integration")]
[Trait("Transport", "Mqtt")]
public class MqttMessageBusIt(ITestOutputHelper output) : BaseIntegrationTest<MqttMessageBusIt>(output)
{
    private const int NumberOfMessages = 77;

    protected override void SetupServices(ServiceCollection services, IConfigurationRoot configuration)
    {
        services.AddSlimMessageBus(mbb =>
        {
            mbb.WithProviderMqtt(cfg =>
            {
                cfg.ClientBuilder
                    .WithTcpServer(Secrets.Service.PopulateSecrets(configuration["Mqtt:Server"]), int.Parse(Secrets.Service.PopulateSecrets(configuration["Mqtt:Port"])))
                    .WithTlsOptions(opts =>
                    {
                        opts.UseTls(bool.TryParse(Secrets.Service.PopulateSecrets(configuration["Mqtt:Secure"]), out var secure) && secure);
                    })
                    .WithCredentials(Secrets.Service.PopulateSecrets(configuration["Mqtt:Username"]), Secrets.Service.PopulateSecrets(configuration["Mqtt:Password"]))
                    // We want to use message headers as part of the tests
                    .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500);
            });

            mbb.AddServicesFromAssemblyContaining<PingConsumer>();
            mbb.AddJsonSerializer();

            ApplyBusConfiguration(mbb);
        });

        services.AddSingleton<TestEventCollector<PingMessage>>();
    }

    public IMessageBus MessageBus => ServiceProvider.GetRequiredService<IMessageBus>();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BasicPubSubOnTopic(bool bulkProduce)
    {
        var concurrency = 2;
        var topic = "test-ping";

        AddBusConfiguration(mbb =>
        {
            mbb
                .Produce<PingMessage>(x => x.DefaultTopic(topic))
                .Consume<PingMessage>(x => x.Topic(topic).Instances(concurrency));
        });

        await BasicPubSub(1, bulkProduce: bulkProduce);
    }

    private async Task BasicPubSub(int expectedMessageCopies, bool bulkProduce)
    {
        // arrange
        var messageBus = MessageBus;
        var consumedMessages = ServiceProvider.GetRequiredService<TestEventCollector<PingMessage>>();

        // act

        // consume all messages that might be on the queue/subscription
        await consumedMessages.WaitUntilArriving(newMessagesTimeout: 4);
        consumedMessages.Clear();
        await WaitUntilConnected();

        // publish
        var stopwatch = Stopwatch.StartNew();

        var producedMessages = Enumerable
            .Range(0, NumberOfMessages)
            .Select(i => new PingMessage(i, Guid.NewGuid()))
            .ToList();

        if (bulkProduce)
        {
            await messageBus.Publish(producedMessages);
        }
        else
        {
            var messageTasks = producedMessages.Select(m => messageBus.Publish(m));
            // wait until all messages are sent
            await Task.WhenAll(messageTasks);
        }

        stopwatch.Stop();
        Logger.LogInformation("Published {MessageCount} messages in {Duration}", producedMessages.Count, stopwatch.Elapsed);

        // consume
        stopwatch.Restart();
        await consumedMessages.WaitUntilArriving(expectedCount: expectedMessageCopies * producedMessages.Count);
        stopwatch.Stop();

        Logger.LogInformation("Consumed {MessageCount} messages in {Duration}", consumedMessages, stopwatch.Elapsed);

        // assert

        // ensure all messages arrived 
        // ... the count should match
        consumedMessages.Count.Should().Be(producedMessages.Count * expectedMessageCopies);
        // ... the content should match
        foreach (var producedMessage in producedMessages)
        {
            var messageCopies = consumedMessages.Snapshot().Count(x => x.Counter == producedMessage.Counter && x.Value == producedMessage.Value);
            messageCopies.Should().Be(expectedMessageCopies);
        }
    }

    [Fact]
    public async Task BasicReqRespOnTopic()
    {
        var topic = "test-echo";

        AddBusConfiguration(mbb =>
        {
            mbb
                .Produce<EchoRequest>(x =>
                {
                    x.DefaultTopic(topic);
                    x.DefaultTimeout(TimeSpan.FromSeconds(30));
                })
                .Handle<EchoRequest, EchoResponse>(x => x.Topic(topic)
                    .WithHandler<EchoRequestHandler>()
                    .Instances(2))
                .ExpectRequestResponses(x =>
                {
                    x.ReplyToTopic("test-echo-resp");
                    x.DefaultTimeout(TimeSpan.FromSeconds(30));
                });
        });

        await BasicReqResp();
    }

    private async Task BasicReqResp()
    {
        // arrange
        var messageBus = MessageBus;

        await EnsureConsumersStarted();
        await WaitUntilConnected();

        // act

        // publish
        var stopwatch = Stopwatch.StartNew();

        var requests = Enumerable
            .Range(0, NumberOfMessages)
            .Select(i => new EchoRequest(i, $"Echo {i}"))
            .ToList();

        var responses = new ConcurrentBag<(EchoRequest Request, EchoResponse Response)>();
        var responseTasks = requests.Select(async req =>
        {
            var resp = await messageBus.Send<EchoResponse, EchoRequest>(req);
            Logger.LogDebug("Received response for index {EchoIndex:000}", req.Index);
            responses.Add((req, resp));
        });
        await Task.WhenAll(responseTasks);

        stopwatch.Stop();
        Logger.LogInformation("Published and received {MessageCount} messages in {Duration}", responses.Count, stopwatch.Elapsed);

        // assert

        // all messages got back
        responses.Count.Should().Be(NumberOfMessages);
        responses.All(x => x.Request.Message == x.Response.Message).Should().BeTrue();
    }

    private async Task WaitUntilConnected()
    {
        // Wait until connected
        var mqttMessageBus = (MqttMessageBus)ServiceProvider.GetRequiredService<IConsumerControl>();
        while (!mqttMessageBus.IsConnected)
        {
            await Task.Delay(200);
        }
    }

    private record PingMessage(int Counter, Guid Value);

    private class PingConsumer(ILogger<PingConsumer> logger, TestEventCollector<PingMessage> messages)
        : IConsumer<PingMessage>, IConsumerWithContext
    {
        private readonly ILogger _logger = logger;
        private readonly TestEventCollector<PingMessage> _messages = messages;

        public IConsumerContext Context { get; set; }

        #region Implementation of IConsumer<in PingMessage>

        public Task OnHandle(PingMessage message, CancellationToken cancellationToken)
        {
            _messages.Add(message);

            _logger.LogInformation("Got message {0} on topic {1}.", message.Counter, Context.Path);
            return Task.CompletedTask;
        }

        #endregion
    }

    private record EchoRequest(int Index, string Message);

    private record EchoResponse(string Message);

    private class EchoRequestHandler : IRequestHandler<EchoRequest, EchoResponse>
    {
        public Task<EchoResponse> OnHandle(EchoRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new EchoResponse(request.Message));
        }
    }
}
