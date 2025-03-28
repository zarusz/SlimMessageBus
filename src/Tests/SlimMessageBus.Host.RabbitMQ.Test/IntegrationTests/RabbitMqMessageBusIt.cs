namespace SlimMessageBus.Host.RabbitMQ.Test.IntegrationTests;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Mime;

using SlimMessageBus;
using SlimMessageBus.Host;
using SlimMessageBus.Host.RabbitMQ;
using SlimMessageBus.Host.Serialization.Json;
using SlimMessageBus.Host.Test.Common.IntegrationTest;

[Trait("Category", "Integration")]
[Trait("Transport", "RabbitMQ")]
public class RabbitMqMessageBusIt(ITestOutputHelper output) : BaseIntegrationTest<RabbitMqMessageBusIt>(output)
{
    private const int NumberOfMessages = 300;

    protected override void SetupServices(ServiceCollection services, IConfigurationRoot configuration)
    {
        services.AddSlimMessageBus((mbb) =>
        {
            mbb.WithProviderRabbitMQ(cfg =>
            {
                cfg.ConnectionString = Secrets.Service.PopulateSecrets(configuration["RabbitMQ:ConnectionString"]);

                // Alternatively, when not using AMQP URI:
                // cfg.ConnectionFactory.HostName = "..."
                // cfg.ConnectionFactory.VirtualHost = "..."
                // cfg.ConnectionFactory.UserName = "..."
                // cfg.ConnectionFactory.Password = "..."
                // cfg.ConnectionFactory.Ssl.Enabled = true

                cfg.ConnectionFactory.ClientProvidedName = $"MyService_{Environment.MachineName}";

                cfg.UseMessagePropertiesModifier((m, p) =>
                {
                    p.ContentType = MediaTypeNames.Application.Json;
                });
                cfg.UseExchangeDefaults(durable: false);
                cfg.UseDeadLetterExchangeDefaults(durable: false, autoDelete: false, exchangeType: ExchangeType.Direct, routingKey: string.Empty);
                cfg.UseQueueDefaults(durable: false);
                cfg.UseTopologyInitializer((channel, applyDefaultTopology) =>
                {
                    // before test clean up
                    channel.QueueDelete("subscriber-0", ifUnused: true, ifEmpty: false);
                    channel.QueueDelete("subscriber-1", ifUnused: true, ifEmpty: false);
                    channel.ExchangeDelete("test-ping", ifUnused: true);
                    channel.ExchangeDelete("subscriber-dlq", ifUnused: true);

                    // apply default SMB inferred topology
                    applyDefaultTopology();

                    // after
                });
            });
            mbb.AddServicesFromAssemblyContaining<PingConsumer>();
            mbb.AddJsonSerializer();
            ApplyBusConfiguration(mbb);
        });

        // Custom error handler
        services.AddTransient(typeof(IRabbitMqConsumerErrorHandler<>), typeof(CustomRabbitMqConsumerErrorHandler<>));

        services.AddSingleton<TestEventCollector<TestEvent>>();
    }

    public IMessageBus MessageBus => ServiceProvider.GetRequiredService<IMessageBus>();

    [Theory]
    [InlineData(RabbitMqMessageAcknowledgementMode.ConfirmAfterMessageProcessingWhenNoManualConfirmMade, 1)]
    [InlineData(RabbitMqMessageAcknowledgementMode.ConfirmAfterMessageProcessingWhenNoManualConfirmMade, 10)]
    [InlineData(RabbitMqMessageAcknowledgementMode.AckAutomaticByRabbit, 1)]
    [InlineData(RabbitMqMessageAcknowledgementMode.AckAutomaticByRabbit, 10)]
    [InlineData(RabbitMqMessageAcknowledgementMode.AckMessageBeforeProcessing, 1)]
    [InlineData(RabbitMqMessageAcknowledgementMode.AckMessageBeforeProcessing, 10)]
    public async Task PubSubOnFanoutExchange(RabbitMqMessageAcknowledgementMode acknowledgementMode, int consumerConcurrency)
    {
        var subscribers = 2;
        var topic = "test-ping";

        AddBusConfiguration(mbb =>
        {
            mbb
                .Produce<PingMessage>(x => x
                    .Exchange(topic, exchangeType: ExchangeType.Fanout)
                    .RoutingKeyProvider((m, p) => m.Value.ToString())
                    .WithHeaderModifier((h, m) =>
                    {
                        // testing string serialization
                        h["Counter"] = m.Counter;
                        // testing bool serialization
                        h["Even"] = m.Counter % 2 == 0;
                    })
                    .MessagePropertiesModifier((m, p) =>
                    {
                        p.MessageId = GetMessageId(m);
                    }))
                .Do(builder => Enumerable.Range(0, subscribers).ToList().ForEach(i =>
                {
                    builder.Consume<PingMessage>(x => x
                        .Queue($"subscriber-{i}", autoDelete: false)
                        .ExchangeBinding(topic)
                        .DeadLetterExchange("subscriber-dlq")
                        .AcknowledgementMode(acknowledgementMode)
                        .WithConsumer<PingConsumer>()
                        .WithConsumer<PingDerivedConsumer, PingDerivedMessage>()
                        .Instances(consumerConcurrency));
                }));
        });

        await BasicPubSub(subscribers, additionalAssertion: testData =>
        {
            testData.ConsumedMessages.Should().AllSatisfy(x => x.ContentType.Should().Be(MediaTypeNames.Application.Json));
            // In the RabbitMQ client there is only one task dispatching the messages to the consumers
            // If we leverage SMB to increase concurrency (instances) then each subscriber (2) will be potentially processed in up to 10 tasks concurrently
            testData.TestMetric.ProcessingCountMax.Should().Be(consumerConcurrency * subscribers);
        });
    }

    private static string GetMessageId(PingMessage message) => $"ID_{message.Counter}";

    public class TestData
    {
        public List<PingMessage> ProducedMessages { get; set; }
        public IReadOnlyCollection<TestEvent> ConsumedMessages { get; set; }
        public TestMetric TestMetric { get; set; }
    }

    private async Task BasicPubSub(int expectedMessageCopies, Action<TestData> additionalAssertion = null)
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

        await Task.WhenAll(producedMessages.Select(x => messageBus.Publish(x)));

        stopwatch.Stop();
        Logger.LogInformation("Published {MessageCount} messages in {Elapsed}", producedMessages.Count, stopwatch.Elapsed);

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

        var messages = consumedMessages.Snapshot();
        messages.All(x => x.Message.Counter == (int)x.Headers["Counter"]).Should().BeTrue();
        messages.All(x => x.Message.Counter % 2 == 0 == (bool)x.Headers["Even"]).Should().BeTrue();

        additionalAssertion?.Invoke(new TestData
        {
            ProducedMessages = producedMessages,
            ConsumedMessages = consumedMessages.Snapshot(),
            TestMetric = testMetric
        });
    }

    [Theory]
    [InlineData(RabbitMqMessageAcknowledgementMode.ConfirmAfterMessageProcessingWhenNoManualConfirmMade)]
    [InlineData(RabbitMqMessageAcknowledgementMode.AckAutomaticByRabbit)]
    [InlineData(RabbitMqMessageAcknowledgementMode.AckMessageBeforeProcessing)]
    public async Task BasicReqRespOnTopic(RabbitMqMessageAcknowledgementMode acknowledgementMode)
    {
        const string topic = "test-echo";

        AddBusConfiguration(mbb =>
        {
            mbb.Produce<EchoRequest>(x =>
            {
                // The requests should be send to "test-echo" exchange
                x.Exchange(topic, exchangeType: ExchangeType.Fanout);
                x.RoutingKeyProvider((m, p) => m.Index.ToString());
                // this is optional
                x.MessagePropertiesModifier((message, transportMessage) =>
                {
                    // set the Azure SB message ID
                    transportMessage.MessageId = $"ID_{message.Index}";
                });
            })
            .Handle<EchoRequest, EchoResponse>(x => x
                // Declare the queue for the handler
                .Queue("echo-request-handler")
                // Bind the queue to the "test-echo" exchange
                .ExchangeBinding("test-echo")
                // If the request handling fails, the failed messages will be routed to the DLQ exchange
                .DeadLetterExchange("echo-request-handler-dlq")
                .AcknowledgementMode(acknowledgementMode)
                .WithHandler<EchoRequestHandler>())
            .ExpectRequestResponses(x =>
            {
                // Tell the handler to which exchange send the responses to
                x.ReplyToExchange("test-echo-resp", ExchangeType.Fanout);
                // Which queue to use to read responses from
                x.Queue("test-echo-resp-queue");
                // Bind to the reply to exchange
                x.ExchangeBinding();
                // Timeout if the response doesn't arrive within 60 seconds
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
        Logger.LogInformation("Published and received {MessageCount} messages in {Elapsed}", responses.Count, stopwatch.Elapsed);

        // assert

        // all messages got back
        responses.Count.Should().Be(NumberOfMessages);
        responses.All(x => x.Item1.Message == x.Item2.Message).Should().BeTrue();
    }
}

public record TestEvent(PingMessage Message, string MessageId, string ContentType, IReadOnlyDictionary<string, object> Headers);

public record PingMessage
{
    public int Counter { get; set; }
    public Guid Value { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public record PingDerivedMessage : PingMessage
{
}

public abstract class AbstractPingConsumer<T> : IConsumer<T>, IConsumerWithContext
    where T : PingMessage
{
    private readonly ILogger _logger;
    private readonly TestEventCollector<TestEvent> _messages;
    private readonly TestMetric _testMetric;

    public AbstractPingConsumer(ILogger<PingConsumer> logger, TestEventCollector<TestEvent> messages, TestMetric testMetric)
    {
        _logger = logger;
        _messages = messages;
        _testMetric = testMetric;
        testMetric.OnCreatedConsumer();
    }

    public IConsumerContext Context { get; set; }

    public async Task OnHandle(T message, CancellationToken cancellationToken)
    {
        _testMetric.OnProcessingStart();
        try
        {
            var transportMessage = Context.GetTransportMessage();

            _messages.Add(new(message, transportMessage.BasicProperties.MessageId, transportMessage.BasicProperties.ContentType, Context.Headers));

            _logger.LogInformation("Got message {Counter:000} on path {Path}.", message.Counter, Context.Path);

            // simulate work
            await Task.Delay(20, cancellationToken);

            await FakeExceptionUtil.SimulateFakeException(message.Counter);
        }
        finally
        {
            _testMetric.OnProcessingFinish();
        }
    }
}


public class PingConsumer(ILogger<PingConsumer> logger, TestEventCollector<TestEvent> messages, TestMetric testMetric)
    : AbstractPingConsumer<PingMessage>(logger, messages, testMetric)
{
}

public class PingDerivedConsumer(ILogger<PingConsumer> logger, TestEventCollector<TestEvent> messages, TestMetric testMetric)
    : AbstractPingConsumer<PingDerivedMessage>(logger, messages, testMetric)
{
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
    {
        return Task.FromResult(new EchoResponse(request.Message));
    }
}

public class FakeErrorException : ApplicationException
{
}

public static class FakeExceptionUtil
{
    public static Task SimulateFakeException(int counter)
    {
        if (counter % 10 == 0)
        {
            throw new FakeErrorException();
        }
        return Task.CompletedTask;
    }
}

/// <summary>
/// Custom Rabbit MQ consumer error handler that acks if the exception is a <see cref="FakeErrorException"/>.
/// </summary>
/// <typeparam name="T"></typeparam>
public class CustomRabbitMqConsumerErrorHandler<T> : IRabbitMqConsumerErrorHandler<T>
{
    public Task<ProcessResult> OnHandleError(T message, IConsumerContext consumerContext, Exception exception, int attempts)
    {
        // Check if this is consumer context for RabbitMQ
        var isRabbitMqContext = consumerContext.GetTransportMessage() != null;
        if (isRabbitMqContext)
        {
            if (exception is FakeErrorException)
            {
                // Ack the message this is just a fake exception
                consumerContext.Ack();
            }
            else
            {
                // For others nack the message to error it out
                consumerContext.Nack();
            }
        }

        return Task.FromResult(isRabbitMqContext
            ? ProcessResult.Success
            : ProcessResult.Failure);
    }
}
