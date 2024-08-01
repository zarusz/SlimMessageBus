namespace SlimMessageBus.Host.Nats.Test;

using System.Collections.Concurrent;
using System.Diagnostics;

using Config;

using Serialization.Json;

[Trait("Category", "Integration")]
[Trait("Transport", "Nats")]
public class NatsMessageBusIt(ITestOutputHelper testOutputHelper) : BaseIntegrationTest<NatsMessageBusIt>(testOutputHelper)
{
    private const int NumberOfMessages = 100;

    protected override void SetupServices(ServiceCollection services, IConfigurationRoot configuration)
    {
        services.AddSlimMessageBus(mbb =>
        {
            mbb.WithProviderNats(cfg =>
            {
                cfg.Endpoint = Secrets.Service.PopulateSecrets(configuration["Nats:Endpoint"]);
                cfg.ClientName = $"MyService_{Environment.MachineName}";
                cfg.AuthOpts = NatsAuthOpts.Default;
            });

            mbb.AddServicesFromAssemblyContaining<PingConsumer>();
            mbb.AddJsonSerializer();

            ApplyBusConfiguration(mbb);
        });

        services.AddSingleton<TestEventCollector<PingMessage>>();
    }

    public IMessageBus MessageBus => ServiceProvider.GetRequiredService<IMessageBus>();

    [Fact]
    public async Task BasicPubSubOnTopic()
    {
        var concurrency = 2;
        var topic = "test-ping";

        AddBusConfiguration(mbb =>
        {
            mbb
                .Produce<PingMessage>(x => x.DefaultTopic(topic))
                .Consume<PingMessage>(x => x.Topic(topic).Instances(concurrency));
        });

        await BasicPubSub(1);
    }

    private async Task BasicPubSub(int expectedMessageCopies)
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

        var messageTasks = producedMessages.Select(m => messageBus.Publish(m));
        // wait until all messages are sent
        await Task.WhenAll(messageTasks);

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
        foreach (var messageCopies in producedMessages.Select(producedMessage =>
                     consumedMessages.Snapshot().Count(x => x.Counter == producedMessage.Counter && x.Value == producedMessage.Value)))
        {
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
        var natsMessageBus = (NatsMessageBus) ServiceProvider.GetRequiredService<IConsumerControl>();
        while (!natsMessageBus.IsConnected)
        {
            await Task.Delay(200);
        }
    }

    private record PingMessage(int Counter, Guid Value);

    private class PingConsumer(ILogger<PingConsumer> logger, TestEventCollector<PingMessage> messages) : IConsumer<PingMessage>, IConsumerWithContext
    {
        private readonly ILogger _logger = logger;

        public IConsumerContext Context { get; set; }

        public Task OnHandle(PingMessage message)
        {
            messages.Add(message);

            _logger.LogInformation("Got message {0} on topic {1}.", message.Counter, Context.Path);
            return Task.CompletedTask;
        }
    }

    private record EchoRequest(int Index, string Message);

    private record EchoResponse(string Message);

    private class EchoRequestHandler : IRequestHandler<EchoRequest, EchoResponse>
    {
        public Task<EchoResponse> OnHandle(EchoRequest request)
        {
            return Task.FromResult(new EchoResponse(request.Message));
        }
    }
}