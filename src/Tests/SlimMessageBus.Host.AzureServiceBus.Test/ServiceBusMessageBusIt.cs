namespace SlimMessageBus.Host.AzureServiceBus.Test;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;

using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using SlimMessageBus.Host;
using SlimMessageBus.Host.Serialization.Json;
using SlimMessageBus.Host.Test.Common.IntegrationTest;

[Trait("Category", "Integration")]
public class ServiceBusMessageBusIt(ITestOutputHelper testOutputHelper) : BaseIntegrationTest<ServiceBusMessageBusIt>(testOutputHelper)
{
    private const int NumberOfMessages = 77;

    private Func<ServiceBusAdministrationClient, Task> CleanTopology { get; set; }

    protected override void SetupServices(ServiceCollection services, IConfigurationRoot configuration)
    {
        services.AddSlimMessageBus((mbb) =>
        {
            mbb.WithProviderServiceBus(cfg =>
            {
                cfg.ConnectionString = Secrets.Service.PopulateSecrets(configuration["Azure:ServiceBus"]);
                cfg.TopologyProvisioning.OnProvisionTopology = async (client, next) =>
                {
                    if (CleanTopology != null)
                    {
                        await CleanTopology(client);
                    }
                    await next();
                };
            });
            mbb.AddServicesFromAssemblyContaining<PingConsumer>();
            mbb.AddJsonSerializer();
            ApplyBusConfiguration(mbb);
        });

        services.AddSingleton<TestEventCollector<TestEvent>>();
        services.AddTransient<CustomPingConsumer>();
    }

    public IMessageBus MessageBus => ServiceProvider.GetRequiredService<IMessageBus>();

    private static void MessageModifier(PingMessage message, ServiceBusMessage sbMessage)
    {
        // set the Azure SB message ID
        sbMessage.MessageId = GetMessageId(message);
        // set the Azure SB message partition key
        sbMessage.PartitionKey = message.Counter.ToString(CultureInfo.InvariantCulture);
    }

    private static void MessageModifierWithSession(PingMessage message, ServiceBusMessage sbMessage)
    {
        // set the Azure SB message ID
        sbMessage.MessageId = GetMessageId(message);
        // set the Azure SB message session id - segment the numbers by their decimal position
        sbMessage.SessionId = $"DecimalDigit_{message.Counter / 10 % 10:00}";
    }

    [Fact]
    public async Task BasicPubSubOnTopic()
    {
        var concurrency = 2;
        var subscribers = 2;
        var topic = "test-ping";

        AddBusConfiguration(mbb =>
        {
            mbb.Produce<PingMessage>(x => x.DefaultTopic(topic).WithModifier(MessageModifier))
                .Do(builder => Enumerable.Range(0, subscribers).ToList().ForEach(i =>
                {
                    builder.Consume<PingMessage>(x => x
                        .Topic(topic)
                        .SubscriptionName($"subscriber-{i}") // ensure subscription exists on the ServiceBus topic
                        .WithConsumer<PingConsumer>()
                        .WithConsumer<PingDerivedConsumer, PingDerivedMessage>()
                        .Instances(concurrency));
                }));
        });

        CleanTopology = async client =>
        {
            await client.DeleteTopicAsync(topic);
        };

        await BasicPubSub(concurrency, subscribers, subscribers);
    }

    [Fact]
    public async Task BasicPubSubOnQueue()
    {
        var concurrency = 2;
        var queue = "test-ping-queue";

        AddBusConfiguration(mbb =>
        {
            mbb
            .Produce<PingMessage>(x => x.DefaultQueue(queue).WithModifier(MessageModifier))
            .Consume<PingMessage>(x => x
                    .Queue(queue)
                    .WithConsumer<PingConsumer>()
                    .WithConsumer<PingDerivedConsumer, PingDerivedMessage>()
                    .Instances(concurrency));
        });

        CleanTopology = async client =>
        {
            await client.DeleteQueueAsync(queue);
        };

        await BasicPubSub(concurrency, 1, 1);
    }

    [Fact]
    public async Task BasicPubSubWithCustomConsumerOnQueue()
    {
        var concurrency = 2;
        var queue = "test-ping-queue";

        AddBusConfiguration(mbb =>
        {
            mbb
            .Produce<PingMessage>(x => x.DefaultQueue(queue).WithModifier(MessageModifier))
            .Consume<PingMessage>(x => x
                    .Queue(queue)
                    .WithConsumer(typeof(CustomPingConsumer), nameof(CustomPingConsumer.Handle))
                    .WithConsumer(typeof(CustomPingConsumer), typeof(PingDerivedMessage), nameof(CustomPingConsumer.Handle))
                    .Instances(concurrency));
        });

        CleanTopology = async client =>
        {
            await client.DeleteQueueAsync(queue);
        };

        await BasicPubSub(concurrency, 1, 1);
    }

    private static string GetMessageId(PingMessage message) => $"ID_{message.Counter}";

    public class TestData
    {
        public List<PingMessage> ProducedMessages { get; set; }
        public IReadOnlyCollection<TestEvent> ConsumedMessages { get; set; }
    }

    private async Task BasicPubSub(int concurrency, int subscribers, int expectedMessageCopies, Action<TestData> additionalAssertion = null)
    {
        // arrange
        var testMetric = ServiceProvider.GetRequiredService<TestMetric>();
        var consumedMessages = ServiceProvider.GetRequiredService<TestEventCollector<TestEvent>>();

        var messageBus = MessageBus;

        // act

        // publish
        var stopwatch = Stopwatch.StartNew();

        var producedMessages = Enumerable
            .Range(0, NumberOfMessages)
            .Select(i => i % 2 == 0 ? new PingMessage { Counter = i } : new PingDerivedMessage { Counter = i })
            .ToList();

        foreach (var producedMessage in producedMessages)
        {
            // Send them in order
            await messageBus.Publish(producedMessage);
        }

        stopwatch.Stop();
        Logger.LogInformation("Published {0} messages in {1}", producedMessages.Count, stopwatch.Elapsed);

        // consume
        stopwatch.Restart();

        await consumedMessages.WaitUntilArriving(newMessagesTimeout: 5);

        stopwatch.Stop();

        // assert

        // ensure number of instances of consumers created matches
        var expectedConsumedCount = producedMessages.Count + producedMessages.OfType<PingDerivedMessage>().Count();
        testMetric.CreatedConsumerCount.Should().Be(expectedConsumedCount * expectedMessageCopies);
        consumedMessages.Count.Should().Be(expectedConsumedCount * expectedMessageCopies);

        // ... the content should match
        foreach (var producedMessage in producedMessages)
        {
            var messageCopies = consumedMessages.Snapshot().Count(x => x.Message.Counter == producedMessage.Counter && x.Message.Value == producedMessage.Value && x.MessageId == GetMessageId(x.Message));
            messageCopies.Should().Be((producedMessage is PingDerivedMessage ? 2 : 1) * expectedMessageCopies);
        }

        additionalAssertion?.Invoke(new TestData { ProducedMessages = producedMessages, ConsumedMessages = consumedMessages.Snapshot() });
    }

    [Fact]
    public async Task BasicReqRespOnTopic()
    {
        var topic = "test-echo";

        AddBusConfiguration(mbb =>
        {
            mbb.Produce<EchoRequest>(x =>
            {
                x.DefaultTopic(topic);
                // this is optional
                x.WithModifier((message, sbMessage) =>
                {
                    // set the Azure SB message ID
                    sbMessage.MessageId = $"ID_{message.Index}";
                    // set the Azure SB message partition key
                    sbMessage.PartitionKey = message.Index.ToString(CultureInfo.InvariantCulture);
                });
            })
            .Handle<EchoRequest, EchoResponse>(x => x.Topic(topic)
                .SubscriptionName("handler")
                .WithHandler<EchoRequestHandler>()
                .Instances(2))
            .ExpectRequestResponses(x =>
            {
                x.ReplyToTopic("test-echo-resp");
                x.SubscriptionName("response-consumer");
                x.DefaultTimeout(TimeSpan.FromSeconds(60));
            });
        });

        await BasicReqResp();
    }

    [Fact]
    public async Task BasicReqRespOnQueue()
    {
        var queue = "test-echo-queue";

        AddBusConfiguration(mbb =>
        {
            mbb.Produce<EchoRequest>(x =>
            {
                x.DefaultQueue(queue);
            })
            .Handle<EchoRequest, EchoResponse>(x => x.Queue(queue)
                .WithHandler<EchoRequestHandler>()
                .Instances(2))
            .ExpectRequestResponses(x =>
            {
                x.ReplyToQueue("test-echo-queue-resp");
                x.DefaultTimeout(TimeSpan.FromSeconds(60));
            });
        });
        await BasicReqResp();
    }

    private async Task BasicReqResp()
    {
        // arrange
        var messageBus = MessageBus;

        // act

        // publish
        var stopwatch = Stopwatch.StartNew();

        var requests = Enumerable
            .Range(0, NumberOfMessages)
            .Select(i => new EchoRequest { Index = i, Message = $"Echo {i}" })
            .ToList();

        var responses = new ConcurrentBag<Tuple<EchoRequest, EchoResponse>>();
        var responseTasks = requests.Select(async req =>
        {
            var resp = await messageBus.Send(req).ConfigureAwait(false);
            responses.Add(Tuple.Create(req, resp));
        });
        await Task.WhenAll(responseTasks).ConfigureAwait(false);

        stopwatch.Stop();
        Logger.LogInformation("Published and received {0} messages in {1}", responses.Count, stopwatch.Elapsed);

        // assert

        // all messages got back
        responses.Count.Should().Be(NumberOfMessages);
        responses.All(x => x.Item1.Message == x.Item2.Message).Should().BeTrue();
    }

    [Fact]
    public async Task FIFOUsingSessionsOnQueue()
    {
        var concurrency = 1;
        var queue = "test-session-queue";

        AddBusConfiguration(mbb =>
        {
            mbb
                .Produce<PingMessage>(x => x.DefaultQueue(queue).WithModifier(MessageModifierWithSession))
                .Consume<PingMessage>(x => x
                        .Queue(queue)
                        .WithConsumer<PingConsumer>()
                        .WithConsumer<PingDerivedConsumer, PingDerivedMessage>()
                        .Instances(concurrency)
                        .EnableSession(x => x.MaxConcurrentSessions(10).SessionIdleTimeout(TimeSpan.FromSeconds(5))));
        });
        await BasicPubSub(concurrency, 1, 1, CheckMessagesWithinSameSessionAreInOrder);
    }

    private static void CheckMessagesWithinSameSessionAreInOrder(TestData testData)
    {
        foreach (var groping in testData.ConsumedMessages.GroupBy(x => x.SessionId))
        {
            var gropingArray = groping.ToArray();
            for (var i = 1; i < gropingArray.Length; i++)
            {
                gropingArray[i - 1].Message.Timestamp.Should().NotBeAfter(gropingArray[i].Message.Timestamp);
            }
        }
    }

    [Fact]
    public async Task FIFOUsingSessionsOnTopic()
    {
        var concurrency = 1;
        var queue = "test-session-topic";

        AddBusConfiguration(mbb =>
        {
            mbb.Produce<PingMessage>(x => x.DefaultTopic(queue).WithModifier(MessageModifierWithSession))
            .Consume<PingMessage>(x => x
                    .Topic(queue)
                    .WithConsumer<PingConsumer>()
                    .WithConsumer<PingDerivedConsumer, PingDerivedMessage>()
                    .Instances(concurrency)
                    .SubscriptionName($"subscriber") // ensure subscription exists on the ServiceBus topic
                    .EnableSession(x => x.MaxConcurrentSessions(10).SessionIdleTimeout(TimeSpan.FromSeconds(5))));
        });

        await BasicPubSub(concurrency, 1, 1, CheckMessagesWithinSameSessionAreInOrder);
    }
}

public record TestEvent(PingMessage Message, string MessageId, string SessionId);

public record PingMessage
{
    public int Counter { get; set; }
    public Guid Value { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public record PingDerivedMessage : PingMessage
{
}

public class PingConsumer : IConsumer<PingMessage>, IConsumerWithContext
{
    private readonly ILogger _logger;
    private readonly TestEventCollector<TestEvent> _messages;

    public PingConsumer(ILogger<PingConsumer> logger, TestEventCollector<TestEvent> messages, TestMetric testMetric)
    {
        _logger = logger;
        _messages = messages;
        testMetric.OnCreatedConsumer();
    }

    public IConsumerContext Context { get; set; }

    #region Implementation of IConsumer<in PingMessage>

    public Task OnHandle(PingMessage message)
    {
        var sbMessage = Context.GetTransportMessage();

        _messages.Add(new(message, sbMessage.MessageId, sbMessage.SessionId));

        _logger.LogInformation("Got message {Counter:000} on path {Path}.", message.Counter, Context.Path);
        return Task.CompletedTask;
    }

    #endregion
}

public class PingDerivedConsumer : IConsumer<PingDerivedMessage>, IConsumerWithContext
{
    private readonly ILogger _logger;
    private readonly TestEventCollector<TestEvent> _messages;

    public PingDerivedConsumer(ILogger<PingDerivedConsumer> logger, TestEventCollector<TestEvent> messages, TestMetric testMetric)
    {
        _logger = logger;
        _messages = messages;
        testMetric.OnCreatedConsumer();
    }

    public IConsumerContext Context { get; set; }

    #region Implementation of IConsumer<in PingMessage>

    public Task OnHandle(PingDerivedMessage message)
    {
        var sbMessage = Context.GetTransportMessage();

        _messages.Add(new(message, sbMessage.MessageId, sbMessage.SessionId));

        _logger.LogInformation("Got message {Counter:000} on path {Path}.", message.Counter, Context.Path);
        return Task.CompletedTask;
    }

    #endregion
}

public class CustomPingConsumer
{
    private readonly ILogger _logger;
    private readonly TestEventCollector<TestEvent> _messages;

    public CustomPingConsumer(ILogger<PingConsumer> logger, TestEventCollector<TestEvent> messages, TestMetric testMetric)
    {
        _logger = logger;
        _messages = messages;
        testMetric.OnCreatedConsumer();
    }

    public Task Handle(PingMessage message, IConsumerContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sbMessage = context.GetTransportMessage();

        _messages.Add(new(message, sbMessage.MessageId, sbMessage.SessionId));

        _logger.LogInformation("Got message {Counter:000} on path {Path}.", message.Counter, context.Path);
        return Task.CompletedTask;
    }

    public Task Handle(PingDerivedMessage message, IConsumerContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sbMessage = context.GetTransportMessage();

        _messages.Add(new(message, sbMessage.MessageId, sbMessage.SessionId));

        _logger.LogInformation("Got message {Counter:000} on path {Path}.", message.Counter, context.Path);
        return Task.CompletedTask;
    }
}

public record EchoRequest : IRequest<EchoResponse>
{
    public int Index { get; set; }
    public string Message { get; set; }
}

public record EchoResponse(string Message);

public class EchoRequestHandler : IRequestHandler<EchoRequest, EchoResponse>
{
    public EchoRequestHandler(TestMetric testMetric)
    {
        testMetric.OnCreatedConsumer();
    }

    public Task<EchoResponse> OnHandle(EchoRequest request)
    {
        return Task.FromResult(new EchoResponse(request.Message));
    }
}