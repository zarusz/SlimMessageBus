namespace SlimMessageBus.Host.Mqtt.Test;

using System.Collections.Concurrent;
using System.Diagnostics;

[Trait("Category", "Integration")]
[Trait("Transport", "MQTT")]
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

    [Fact]
    public async Task When_PublishIsCalled_Given_HeadersAreProvided_Then_HeadersAreTransmitted()
    {
        // Arrange
        var topic = "test-ping-with-headers";
        var capturedHeaders = new ConcurrentBag<IDictionary<string, object>>();

        AddBusConfiguration(mbb =>
        {
            mbb
                .Produce<PingMessage>(x => x.DefaultTopic(topic))
                .Consume<PingMessage>(x => x.Topic(topic).Instances(1));
        });

        await EnsureConsumersStarted();
        await WaitUntilConnected();

        var consumedMessages = ServiceProvider.GetRequiredService<TestEventCollector<PingMessage>>();
        await consumedMessages.WaitUntilArriving(newMessagesTimeout: 2);
        consumedMessages.Clear();

        // Act
        var message = new PingMessage(1, Guid.NewGuid());
        var headers = new Dictionary<string, object>
        {
            ["CustomHeader1"] = "Value1",
            ["CustomHeader2"] = "Value2"
        };
        await MessageBus.Publish(message, path: null, headers: headers);

        // Assert
        await consumedMessages.WaitUntilArriving(newMessagesTimeout: 10, expectedCount: 1);
        consumedMessages.Count.Should().Be(1);
    }

    [Fact]
    public async Task When_PublishIsCalled_Given_NoHeadersProvided_Then_MessageIsDelivered()
    {
        // Arrange
        var topic = "test-ping-no-headers";

        AddBusConfiguration(mbb =>
        {
            mbb
                .Produce<PingMessage>(x => x.DefaultTopic(topic))
                .Consume<PingMessage>(x => x.Topic(topic).Instances(1));
        });

        await EnsureConsumersStarted();
        await WaitUntilConnected();

        var consumedMessages = ServiceProvider.GetRequiredService<TestEventCollector<PingMessage>>();
        await consumedMessages.WaitUntilArriving(newMessagesTimeout: 2);
        consumedMessages.Clear();

        // Act - Publish without explicit headers
        var message = new PingMessage(42, Guid.NewGuid());
        await MessageBus.Publish(message);

        // Assert
        await consumedMessages.WaitUntilArriving(newMessagesTimeout: 10, expectedCount: 1);
        consumedMessages.Count.Should().Be(1);
        consumedMessages.Snapshot().First().Counter.Should().Be(42);
    }

    [Fact]
    public async Task When_PublishIsCalled_Given_MessageModifierIsConfigured_Then_ModifierIsCalled()
    {
        // Arrange
        var topic = "test-ping-with-modifier";
        var modifierWasCalled = false;

        AddBusConfiguration(mbb =>
        {
            mbb
                .Produce<PingMessage>(x => x
                    .DefaultTopic(topic)
                    .WithModifier((message, mqttMessage) =>
                    {
                        modifierWasCalled = true;
                        mqttMessage.ResponseTopic = "custom-response-topic";
                        mqttMessage.MessageExpiryInterval = 60;
                    }))
                .Consume<PingMessage>(x => x.Topic(topic).Instances(1));
        });

        await EnsureConsumersStarted();
        await WaitUntilConnected();

        var consumedMessages = ServiceProvider.GetRequiredService<TestEventCollector<PingMessage>>();
        await consumedMessages.WaitUntilArriving(newMessagesTimeout: 2);
        consumedMessages.Clear();

        // Act
        var message = new PingMessage(99, Guid.NewGuid());
        await MessageBus.Publish(message);

        // Assert
        await consumedMessages.WaitUntilArriving(newMessagesTimeout: 10, expectedCount: 1);
        modifierWasCalled.Should().BeTrue();
        consumedMessages.Count.Should().Be(1);
    }

    [Fact]
    public async Task When_PublishIsCalled_Given_MessageModifierThrows_Then_MessageIsStillPublished()
    {
        // Arrange
        var topic = "test-ping-modifier-throws";

        AddBusConfiguration(mbb =>
        {
            mbb
                .Produce<PingMessage>(x => x
                    .DefaultTopic(topic)
                    .WithModifier((message, mqttMessage) =>
                    {
                        // This should be caught and logged but not prevent publishing
                        throw new InvalidOperationException("Test exception in modifier");
                    }))
                .Consume<PingMessage>(x => x.Topic(topic).Instances(1));
        });

        await EnsureConsumersStarted();
        await WaitUntilConnected();

        var consumedMessages = ServiceProvider.GetRequiredService<TestEventCollector<PingMessage>>();
        await consumedMessages.WaitUntilArriving(newMessagesTimeout: 2);
        consumedMessages.Clear();

        // Act - Should not throw exception
        var message = new PingMessage(123, Guid.NewGuid());
        await MessageBus.Publish(message);

        // Assert - Message should still be delivered despite modifier exception
        await consumedMessages.WaitUntilArriving(newMessagesTimeout: 10, expectedCount: 1);
        consumedMessages.Count.Should().Be(1);
    }

    [Fact]
    public async Task When_BusStarts_Given_NoConsumersConfigured_Then_StartsSuccessfully()
    {
        // Arrange - Configure bus with NO consumers
        AddBusConfiguration(mbb =>
        {
            mbb.Produce<PingMessage>(x => x.DefaultTopic("test-no-consumers"));
            // Intentionally not adding any consumers
        });

        // Act
        var act = async () =>
        {
            await EnsureConsumersStarted();
            var messageBus = ServiceProvider.GetRequiredService<IMessageBus>();
            await messageBus.Publish(new PingMessage(1, Guid.NewGuid()));
        };

        // Assert - Should not throw
        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(0)] // QoS 0
    [InlineData(1)] // QoS 1
    public async Task When_PublishIsCalled_Given_DifferentQoSLevels_Then_MessageIsDelivered(byte qos)
    {
        // Arrange
        var topic = $"test-qos-{qos}";

        AddBusConfiguration(mbb =>
        {
            mbb
                .Produce<PingMessage>(x => x
                    .DefaultTopic(topic)
                    .WithModifier((message, mqttMessage) =>
                    {
                        mqttMessage.QualityOfServiceLevel = (MQTTnet.Protocol.MqttQualityOfServiceLevel)qos;
                    }))
                .Consume<PingMessage>(x => x.Topic(topic).Instances(1));
        });

        await EnsureConsumersStarted();
        await WaitUntilConnected();

        var consumedMessages = ServiceProvider.GetRequiredService<TestEventCollector<PingMessage>>();
        await consumedMessages.WaitUntilArriving(newMessagesTimeout: 2);
        consumedMessages.Clear();

        // Act
        var message = new PingMessage(qos, Guid.NewGuid());
        await MessageBus.Publish(message);

        // Assert
        await consumedMessages.WaitUntilArriving(newMessagesTimeout: 10, expectedCount: 1);
        consumedMessages.Count.Should().Be(1);
        consumedMessages.Snapshot().First().Counter.Should().Be(qos);
    }
}
