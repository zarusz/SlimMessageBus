namespace SlimMessageBus.Host.AzureServiceBus.Test;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using SlimMessageBus.Host;
using SlimMessageBus.Host.Interceptor;
using SlimMessageBus.Host.Serialization.Json;
using SlimMessageBus.Host.Test.Common.IntegrationTest;

/// <summary>
/// Runs the integration tests for the <see cref="ServiceBusMessageBus"/>.
/// Notice that this test needs to run against a real Azure Service Bus infrastructure.
/// Inside the GitHub Actions pipeline, the Azure Service Bus infrastructure is shared, and this tests attempts to isolate itself by using unique queue/topic names.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Transport", "AzureServiceBus")]
public class ServiceBusMessageBusIt(ITestOutputHelper output)
    : BaseIntegrationTest<ServiceBusMessageBusIt>(output)
{
    private const int NumberOfMessages = 100;

    protected override void SetupServices(ServiceCollection services, IConfigurationRoot configuration)
    {
        services.AddSlimMessageBus((mbb) =>
        {
            mbb.WithProviderServiceBus(cfg =>
            {
                cfg.ConnectionString = Secrets.Service.PopulateSecrets(configuration["Azure:ServiceBus"]);
                cfg.PrefetchCount = 100;
                cfg.MaxConcurrentSessions = 20;
                // auto-delete queues and topics after 5 minutes, we create temp topics and queues
                cfg.TopologyProvisioning.CreateQueueOptions = o => o.AutoDeleteOnIdle = TimeSpan.FromMinutes(5);
                cfg.TopologyProvisioning.CreateTopicOptions = o => o.AutoDeleteOnIdle = TimeSpan.FromMinutes(5);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BasicPubSubOnTopic(bool bulkProduce)
    {
        var subscribers = 2;
        var topic = TopicName();
        var expectedSubscriptionPrefix = "subscriber";

        AddBusConfiguration(mbb =>
        {
            mbb.Produce<PingMessage>(x => x.DefaultTopic(topic).WithModifier(MessageModifier))
                .Do(builder => Enumerable.Range(0, subscribers).ToList().ForEach(i =>
                {
                    builder.Consume<PingMessage>(x => x
                        .Topic(topic)
                        .SubscriptionName($"{expectedSubscriptionPrefix}-{i}") // ensure subscription exists on the ServiceBus topic
                        .WithConsumer<PingConsumer>()
                        .WithConsumer<PingDerivedConsumer, PingDerivedMessage>()
                        .Instances(20));
                }));
        });

        await BasicPubSub(subscribers, bulkProduce: bulkProduce, expectedSubscriptionPrefix: expectedSubscriptionPrefix);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BasicPubSubOnQueue(bool bulkProduce)
    {
        var queue = QueueName();

        AddBusConfiguration(mbb =>
        {
            mbb
            .Produce<PingMessage>(x => x.DefaultQueue(queue).WithModifier(MessageModifier))
            .Consume<PingMessage>(x => x
                    .Queue(queue)
                    .WithConsumer<PingConsumer>()
                    .WithConsumer<PingDerivedConsumer, PingDerivedMessage>()
                    .Instances(20));
        });

        await BasicPubSub(1, bulkProduce: bulkProduce, expectedSubscriptionPrefix: null);
    }

    [Fact]
    public async Task Failure_WithProperties_UpdatesServiceBusMessage()
    {
        // arrange
        var queue = QueueName();

        var expectedProperties = new Dictionary<string, object>
        {
            { "key", "value" },
            { "SMB.Exception", "Do not overwrite" }
        };

        AddTestServices((services, configuration) =>
        {
            services.AddTransient(sp =>
            {
                var connectionString = Secrets.Service.PopulateSecrets(configuration["Azure:ServiceBus"]);
                return ActivatorUtilities.CreateInstance<ServiceBusClient>(sp, connectionString);
            });

            services.AddTransient(sp =>
            {
                var connectionString = Secrets.Service.PopulateSecrets(configuration["Azure:ServiceBus"]);
                return ActivatorUtilities.CreateInstance<ServiceBusAdministrationClient>(sp, connectionString);
            });

            services.AddScoped(typeof(IConsumerInterceptor<>), typeof(ThrowExceptionPingMessageInterceptor<>));
            services.AddSingleton<IServiceBusConsumerErrorHandler<PingMessage>>(_ => new DelegatedConsumerErrorHandler<PingMessage>((handler, message, context, exception, attempts) => handler.Failure(expectedProperties)));
        });

        AddBusConfiguration(mbb =>
        {
            mbb
            .Produce<PingMessage>(x => x.DefaultQueue(queue).WithModifier(MessageModifier))
            .Consume<PingMessage>(x => x
                    .Queue(queue)
                    .CreateQueueOptions(z => z.MaxDeliveryCount = 1)
                    .WithConsumer<PingConsumer>()
                    .Instances(20));
        });

        var adminClient = ServiceProvider.GetRequiredService<ServiceBusAdministrationClient>();
        var testMetric = ServiceProvider.GetRequiredService<TestMetric>();
        var consumedMessages = ServiceProvider.GetRequiredService<TestEventCollector<TestEvent>>();
        var client = ServiceProvider.GetRequiredService<ServiceBusClient>();
        var deadLetterReceiver = client.CreateReceiver($"{queue}/$DeadLetterQueue");

        // act
        var messageBus = MessageBus;

        var producedMessages = Enumerable
            .Range(0, NumberOfMessages)
            .Select(i => new PingMessage { Counter = i })
            .ToList();

        await messageBus.Publish(producedMessages);
        await consumedMessages.WaitUntilArriving(newMessagesTimeout: 5);

        // assert
        // ensure number of instances of consumers created matches
        testMetric.CreatedConsumerCount.Should().Be(NumberOfMessages);
        consumedMessages.Count.Should().Be(NumberOfMessages);

        // all messages should be in the DLQ
        var properties = await adminClient.GetQueueRuntimePropertiesAsync(queue);
        properties.Value.ActiveMessageCount.Should().Be(0);
        properties.Value.DeadLetterMessageCount.Should().Be(NumberOfMessages);

        // all messages should have been sent directly to the DLQ
        var messages = await deadLetterReceiver.PeekMessagesAsync(NumberOfMessages);
        messages.Count.Should().Be(NumberOfMessages);
        foreach (var message in messages)
        {
            message.DeliveryCount.Should().Be(1);
            foreach (var property in expectedProperties)
            {
                message.ApplicationProperties[property.Key].Should().Be(property.Value);
            }
        }
    }

    [Fact]
    public async Task DeadLetterMessageWithoutReasonOrDescription_IsDeliveredTo_DeadLetterQueueWithExceptionDetails()
    {
        // arrange
        var queue = QueueName();

        AddTestServices((services, configuration) =>
        {
            services.AddTransient(sp =>
            {
                var connectionString = Secrets.Service.PopulateSecrets(configuration["Azure:ServiceBus"]);
                return ActivatorUtilities.CreateInstance<ServiceBusClient>(sp, connectionString);
            });

            services.AddTransient(sp =>
            {
                var connectionString = Secrets.Service.PopulateSecrets(configuration["Azure:ServiceBus"]);
                return ActivatorUtilities.CreateInstance<ServiceBusAdministrationClient>(sp, connectionString);
            });

            services.AddScoped(typeof(IConsumerInterceptor<>), typeof(ThrowExceptionPingMessageInterceptor<>));
            services.AddSingleton<IServiceBusConsumerErrorHandler<PingMessage>>(_ => new DelegatedConsumerErrorHandler<PingMessage>((handler, message, context, exception, attempts) => handler.DeadLetter()));
        });

        AddBusConfiguration(mbb =>
        {
            mbb
            .Produce<PingMessage>(x => x.DefaultQueue(queue).WithModifier(MessageModifier))
            .Consume<PingMessage>(x => x
                    .Queue(queue)
                    .WithConsumer<PingConsumer>()
                    .Instances(20));
        });

        var adminClient = ServiceProvider.GetRequiredService<ServiceBusAdministrationClient>();
        var testMetric = ServiceProvider.GetRequiredService<TestMetric>();
        var consumedMessages = ServiceProvider.GetRequiredService<TestEventCollector<TestEvent>>();
        var client = ServiceProvider.GetRequiredService<ServiceBusClient>();
        var deadLetterReceiver = client.CreateReceiver($"{queue}/$DeadLetterQueue");

        // act
        var messageBus = MessageBus;

        var producedMessages = Enumerable
            .Range(0, NumberOfMessages)
            .Select(i => new PingMessage { Counter = i })
            .ToList();

        await messageBus.Publish(producedMessages);
        await consumedMessages.WaitUntilArriving(newMessagesTimeout: 5);

        // assert
        // ensure number of instances of consumers created matches
        testMetric.CreatedConsumerCount.Should().Be(NumberOfMessages);
        consumedMessages.Count.Should().Be(NumberOfMessages);

        // all messages should be in the DLQ
        var properties = await adminClient.GetQueueRuntimePropertiesAsync(queue);
        properties.Value.ActiveMessageCount.Should().Be(0);
        properties.Value.DeadLetterMessageCount.Should().Be(NumberOfMessages);

        // all messages should have been sent directly to the DLQ
        var messages = await deadLetterReceiver.PeekMessagesAsync(NumberOfMessages);
        messages.Count.Should().Be(NumberOfMessages);
        foreach (var message in messages)
        {
            message.DeliveryCount.Should().Be(0);
            message.ApplicationProperties["DeadLetterReason"].Should().Be(nameof(ApplicationException));
        }
    }

    [Fact]
    public async Task DeadLetterMessageWithReasonAndDescription_IsDeliveredTo_DeadLetterQueueWithReasonAndDescription()
    {
        // arrange
        const string expectedReason = "Reason";
        const string expectedDescription = "Description";

        var queue = QueueName();

        AddTestServices((services, configuration) =>
        {
            services.AddTransient(sp =>
            {
                var connectionString = Secrets.Service.PopulateSecrets(configuration["Azure:ServiceBus"]);
                return ActivatorUtilities.CreateInstance<ServiceBusClient>(sp, connectionString);
            });

            services.AddTransient(sp =>
            {
                var connectionString = Secrets.Service.PopulateSecrets(configuration["Azure:ServiceBus"]);
                return ActivatorUtilities.CreateInstance<ServiceBusAdministrationClient>(sp, connectionString);
            });

            services.AddScoped(typeof(IConsumerInterceptor<>), typeof(ThrowExceptionPingMessageInterceptor<>));
            services.AddSingleton<IServiceBusConsumerErrorHandler<PingMessage>>(_ => new DelegatedConsumerErrorHandler<PingMessage>((handler, message, context, exception, attempts) => handler.DeadLetter(expectedReason, expectedDescription)));
        });

        AddBusConfiguration(mbb =>
        {
            mbb
            .Produce<PingMessage>(x => x.DefaultQueue(queue).WithModifier(MessageModifier))
            .Consume<PingMessage>(x => x
                    .Queue(queue)
                    .WithConsumer<PingConsumer>()
                    .Instances(20));
        });

        var adminClient = ServiceProvider.GetRequiredService<ServiceBusAdministrationClient>();
        var testMetric = ServiceProvider.GetRequiredService<TestMetric>();
        var consumedMessages = ServiceProvider.GetRequiredService<TestEventCollector<TestEvent>>();
        var client = ServiceProvider.GetRequiredService<ServiceBusClient>();
        var deadLetterReceiver = client.CreateReceiver($"{queue}/$DeadLetterQueue");

        // act
        var messageBus = MessageBus;

        var producedMessages = Enumerable
            .Range(0, NumberOfMessages)
            .Select(i => new PingMessage { Counter = i })
            .ToList();

        await messageBus.Publish(producedMessages);
        await consumedMessages.WaitUntilArriving(newMessagesTimeout: 5);

        // assert
        // ensure number of instances of consumers created matches
        testMetric.CreatedConsumerCount.Should().Be(NumberOfMessages);
        consumedMessages.Count.Should().Be(NumberOfMessages);

        // all messages should be in the DLQ
        var properties = await adminClient.GetQueueRuntimePropertiesAsync(queue);
        properties.Value.ActiveMessageCount.Should().Be(0);
        properties.Value.DeadLetterMessageCount.Should().Be(NumberOfMessages);

        // all messages should have been sent directly to the DLQ
        var messages = await deadLetterReceiver.PeekMessagesAsync(NumberOfMessages);
        messages.Count.Should().Be(NumberOfMessages);
        foreach (var message in messages)
        {
            message.DeliveryCount.Should().Be(0);
            message.ApplicationProperties["DeadLetterReason"].Should().Be(expectedReason);
            message.ApplicationProperties["DeadLetterErrorDescription"].Should().Be(expectedDescription);
        }
    }

    public class ThrowExceptionPingMessageInterceptor<T> : IConsumerInterceptor<T>
    {
        public async Task<object> OnHandle(T message, Func<Task<object>> next, IConsumerContext context)
        {
            await next();
            var pingMessage = message as PingMessage;
            throw new ApplicationException($"Abandon message {pingMessage.Counter:000} on path {context.Path}.");
        }
    }

    public class DelegatedConsumerErrorHandler<T>(DelegatedConsumerErrorHandler<T>.OnHandleErrorDelegate onHandleError) : ServiceBusConsumerErrorHandler<T>
    {
        private readonly OnHandleErrorDelegate _onHandleError = onHandleError;

        public override Task<ProcessResult> OnHandleError(T message, IConsumerContext consumerContext, Exception exception, int attempts)
        {
            return Task.FromResult(_onHandleError(this, message, consumerContext, exception, attempts));
        }

        public delegate ProcessResult OnHandleErrorDelegate(ServiceBusConsumerErrorHandler<T> handler, T message, IConsumerContext contrext, Exception exception, int attempts);
    }

    [Fact]
    public async Task BasicPubSubWithCustomConsumerOnQueue()
    {
        var queue = QueueName();

        AddBusConfiguration(mbb =>
        {
            mbb
            .Produce<PingMessage>(x => x.DefaultQueue(queue).WithModifier(MessageModifier))
            .Consume<PingMessage>(x => x
                    .Queue(queue)
                    .WithConsumer(typeof(CustomPingConsumer), nameof(CustomPingConsumer.Handle))
                    .WithConsumer(typeof(CustomPingConsumer), typeof(PingDerivedMessage), nameof(CustomPingConsumer.Handle))
                    .Instances(20));
        });

        await BasicPubSub(1, expectedSubscriptionPrefix: null);
    }

    private static string GetMessageId(PingMessage message) => $"ID_{message.Counter}";

    public class TestData
    {
        public List<PingMessage> ProducedMessages { get; set; }
        public IReadOnlyCollection<TestEvent> ConsumedMessages { get; set; }
    }

    private async Task BasicPubSub(int expectedMessageCopies, Action<TestData> additionalAssertion = null, bool bulkProduce = false, string expectedSubscriptionPrefix = null)
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

        if (bulkProduce)
        {
            await messageBus.Publish(producedMessages);
        }
        else
        {
            foreach (var producedMessage in producedMessages)
            {
                // Send them in order
                await messageBus.Publish(producedMessage);
            }
        }

        stopwatch.Stop();
        Logger.LogInformation("Published {Count} messages in {Elapsed}", producedMessages.Count, stopwatch.Elapsed);

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
            var messageCopies = consumedMessages.Snapshot()
                .Count(x => x.Message.Counter == producedMessage.Counter
                    && x.Message.Value == producedMessage.Value && x.MessageId == GetMessageId(x.Message)
                    && (expectedSubscriptionPrefix == null || x.SubscriptionName.StartsWith(expectedSubscriptionPrefix, StringComparison.InvariantCulture)));

            messageCopies.Should().Be((producedMessage is PingDerivedMessage ? 2 : 1) * expectedMessageCopies);
        }

        additionalAssertion?.Invoke(new TestData { ProducedMessages = producedMessages, ConsumedMessages = consumedMessages.Snapshot() });
    }

    [Fact]
    public async Task BasicReqRespOnTopic()
    {
        var topic = TopicName();
        var responseTopic = ($"{topic}-resp");

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
                .Instances(20))
            .ExpectRequestResponses(x =>
            {
                x.ReplyToTopic(responseTopic);
                x.SubscriptionName("response-consumer");
                x.DefaultTimeout(TimeSpan.FromSeconds(60));
            });
        });

        await BasicReqResp();
    }

    [Fact]
    public async Task BasicReqRespOnQueue()
    {
        var queue = QueueName();
        var responseQueue = $"{queue}-resp";

        AddBusConfiguration(mbb =>
        {
            mbb.Produce<EchoRequest>(x =>
            {
                x.DefaultQueue(queue);
            })
            .Handle<EchoRequest, EchoResponse>(x => x.Queue(queue)
                .WithHandler<EchoRequestHandler>()
                .Instances(20))
            .ExpectRequestResponses(x =>
            {
                x.ReplyToQueue(responseQueue);
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
        Logger.LogInformation("Published and received {Count} messages in {Elapsed}", responses.Count, stopwatch.Elapsed);

        // assert

        // all messages got back
        responses.Count.Should().Be(NumberOfMessages);
        responses.All(x => x.Item1.Message == x.Item2.Message).Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FIFOUsingSessionsOnQueue(bool bulkProduce)
    {
        var queue = QueueName();

        AddBusConfiguration(mbb =>
        {
            mbb
                .Produce<PingMessage>(x => x.DefaultQueue(queue).WithModifier(MessageModifierWithSession))
                .Consume<PingMessage>(x => x
                        .Queue(queue)
                        .WithConsumer<PingConsumer>()
                        .WithConsumer<PingDerivedConsumer, PingDerivedMessage>()
                        .Instances(1)
                        .EnableSession(x => x.MaxConcurrentSessions(10).SessionIdleTimeout(TimeSpan.FromSeconds(5))));
        });
        await BasicPubSub(1, CheckMessagesWithinSameSessionAreInOrder, bulkProduce: bulkProduce);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FIFOUsingSessionsOnTopic(bool bulkProduce)
    {
        var queue = QueueName();

        AddBusConfiguration(mbb =>
        {
            mbb.Produce<PingMessage>(x => x.DefaultTopic(queue).WithModifier(MessageModifierWithSession))
            .Consume<PingMessage>(x => x
                    .Topic(queue)
                    .WithConsumer<PingConsumer>()
                    .WithConsumer<PingDerivedConsumer, PingDerivedMessage>()
                    .Instances(1)
                    .SubscriptionName($"subscriber") // ensure subscription exists on the ServiceBus topic
                    .EnableSession(x => x.MaxConcurrentSessions(10).SessionIdleTimeout(TimeSpan.FromSeconds(5))));
        });

        await BasicPubSub(1, CheckMessagesWithinSameSessionAreInOrder, bulkProduce: bulkProduce);
    }

    private static string QueueName([CallerMemberName] string testName = null)
        => $"smb-tests/{nameof(ServiceBusMessageBusIt)}/{testName}/{DateTimeOffset.UtcNow.Ticks}";

    private static string TopicName([CallerMemberName] string testName = null)
        => QueueName(testName);
}

public record TestEvent(PingMessage Message, string MessageId, string SessionId, string SubscriptionName);

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

    public Task OnHandle(PingMessage message, CancellationToken cancellationToken)
    {
        var sbMessage = Context.GetTransportMessage();
        var subscriptionName = Context.GetSubscriptionName();

        _messages.Add(new(message, sbMessage.MessageId, sbMessage.SessionId, subscriptionName));

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

    public Task OnHandle(PingDerivedMessage message, CancellationToken cancellationToken)
    {
        var sbMessage = Context.GetTransportMessage();
        var subscriptionName = Context.GetSubscriptionName();

        _messages.Add(new(message, sbMessage.MessageId, sbMessage.SessionId, subscriptionName));

        _logger.LogInformation("Got message {Counter:000} on path {Path}.", message.Counter, Context.Path);
        return Task.CompletedTask;
    }
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
        var subscriptionName = context.GetSubscriptionName();

        _messages.Add(new(message, sbMessage.MessageId, sbMessage.SessionId, subscriptionName));

        _logger.LogInformation("Got message {Counter:000} on path {Path}.", message.Counter, context.Path);
        return Task.CompletedTask;
    }

    public Task Handle(PingDerivedMessage message, IConsumerContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sbMessage = context.GetTransportMessage();
        var subscriptionName = context.GetSubscriptionName();

        _messages.Add(new(message, sbMessage.MessageId, sbMessage.SessionId, subscriptionName));

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

    public Task<EchoResponse> OnHandle(EchoRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new EchoResponse(request.Message));
}