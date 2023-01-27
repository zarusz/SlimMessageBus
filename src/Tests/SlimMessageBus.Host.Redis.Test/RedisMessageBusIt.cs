namespace SlimMessageBus.Host.Redis.Test;

using System.Diagnostics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using SlimMessageBus.Host.DependencyResolver;
using SlimMessageBus.Host.MsDependencyInjection;
using SlimMessageBus.Host.Serialization.Json;
using SlimMessageBus.Host.Test.Common;

[Trait("Category", "Integration")]
public class RedisMessageBusIt : BaseIntegrationTest<RedisMessageBusIt>
{
    private const int NumberOfMessages = 77;

    public RedisMessageBusIt(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
    {
    }

    protected override void SetupServices(ServiceCollection services, IConfigurationRoot configuration)
    {
        services.AddSlimMessageBus(mbb =>
        {
            var connectionString = Secrets.Service.PopulateSecrets(configuration["Redis:ConnectionString"]);

            mbb
                .WithSerializer(new JsonMessageSerializer())
                .WithProviderRedis(new RedisMessageBusSettings(connectionString)
                {
                    OnDatabaseConnected = (database) =>
                    {
                        // Upon connect clear the redis list with the specified keys
                        database.KeyDelete("test-echo-queue");
                        database.KeyDelete("test-echo-queue-resp");
                    }
                });

            ApplyBusConfiguration(mbb);
        });
    }

    public IMessageBus MessageBus => ServiceProvider.GetRequiredService<IMessageBus>();

    [Fact]
    public async Task BasicPubSubOnTopic()
    {
        var concurrency = 2;
        var consumers = 2;
        var topic = "test-ping";

        AddBusConfiguration(mbb =>
        {
            mbb
                .Produce<PingMessage>(x =>
                {
                    x.DefaultTopic(topic);
                })
                .Do(builder => Enumerable.Range(0, consumers).ToList().ForEach(i =>
                {
                    builder.Consume<PingMessage>(x => x
                        .Topic(topic)
                        .WithConsumer<PingConsumer>()
                        .Instances(concurrency));
                }));
        });

        await BasicPubSub(consumers).ConfigureAwait(false);
    }

    [Fact]
    public async Task BasicPubSubOnQueue()
    {
        var concurrency = 2;
        var consumers = 2;
        var queue = "test-ping-queue";

        AddBusConfiguration(mbb =>
        {
            mbb
                .Produce<PingMessage>(x =>
                {
                    x.DefaultQueue(queue);
                })
                .Do(builder => Enumerable.Range(0, consumers).ToList().ForEach(i =>
                {
                    builder.Consume<PingMessage>(x => x
                        .Queue(queue)
                        .WithConsumer<PingConsumer>()
                        .Instances(concurrency));
                }));
        });

        await BasicPubSub(consumers).ConfigureAwait(false);
    }

    private async Task BasicPubSub(int expectedMessageCopies)
    {
        // arrange
        var pingConsumer = new PingConsumer(LoggerFactory.CreateLogger<PingConsumer>());

        AddBusConfiguration(mbb =>
        {
            mbb
                .WithDependencyResolver(new LookupDependencyResolver(f =>
                {
                    if (f == typeof(PingConsumer)) return pingConsumer;
                    if (f == typeof(ILoggerFactory)) return null;
                    // for interceptors
                    if (f.IsGenericType && f.GetGenericTypeDefinition() == typeof(IEnumerable<>)) return Enumerable.Empty<object>();
                    if (f == typeof(ILoggerFactory)) return LoggerFactory;
                    throw new InvalidOperationException();
                }));
        });

        var messageBus = MessageBus;

        // ensure the consumers are warm
        //while (!messageBus.IsStarted) await Task.Delay(200);

        // act

        // consume all messages that might be on the queue/subscription
        await ConsumeAll(pingConsumer, null, 2);
        pingConsumer.Messages.Clear();

        // publish
        var stopwatch = Stopwatch.StartNew();

        var producedMessages = Enumerable
            .Range(0, NumberOfMessages)
            .Select(i => new PingMessage { Counter = i, Value = Guid.NewGuid() })
            .ToList();

        var messageTasks = producedMessages.Select(m => messageBus.Publish(m));
        // wait until all messages are sent
        await Task.WhenAll(messageTasks).ConfigureAwait(false);

        stopwatch.Stop();
        Logger.LogInformation("Published {0} messages in {1}", producedMessages.Count, stopwatch.Elapsed);

        // consume
        stopwatch.Restart();
        var consumersReceivedMessages = await ConsumeAll(pingConsumer, expectedMessageCopies * producedMessages.Count);
        stopwatch.Stop();

        Logger.LogInformation("Consumed {0} messages in {1}", consumersReceivedMessages.Count, stopwatch.Elapsed);

        // assert

        // ensure all messages arrived 
        // ... the count should match
        consumersReceivedMessages.Count.Should().Be(producedMessages.Count * expectedMessageCopies);
        // ... the content should match
        foreach (var producedMessage in producedMessages)
        {
            var messageCopies = consumersReceivedMessages.Count(x => x.Counter == producedMessage.Counter && x.Value == producedMessage.Value);
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

        await BasicReqResp().ConfigureAwait(false);
    }

    [Fact]
    public async Task BasicReqRespOnQueue()
    {
        var queue = "test-echo-queue";

        AddBusConfiguration(mbb =>
        {
            mbb
                .Produce<EchoRequest>(x =>
                {
                    x.DefaultQueue(queue);
                    x.DefaultTimeout(TimeSpan.FromSeconds(30));
                })
                .Handle<EchoRequest, EchoResponse>(x => x.Queue(queue)
                    .WithHandler<EchoRequestHandler>()
                    .Instances(2))
                .ExpectRequestResponses(x =>
                {
                    x.ReplyToQueue("test-echo-queue-resp");
                    x.DefaultTimeout(TimeSpan.FromSeconds(30));
                });
        });

        await BasicReqResp().ConfigureAwait(false);
    }

    private async Task BasicReqResp()
    {
        // arrange
        var consumer = new EchoRequestHandler();

        AddBusConfiguration(mbb =>
        {
            mbb
                .WithDependencyResolver(new LookupDependencyResolver(f =>
                {
                    if (f == typeof(EchoRequestHandler)) return consumer;
                    // for interceptors
                    if (f.IsGenericType && f.GetGenericTypeDefinition() == typeof(IEnumerable<>)) return Enumerable.Empty<object>();
                    if (f == typeof(ILoggerFactory)) return LoggerFactory;
                    throw new InvalidOperationException();
                }));
        });

        var messageBus = MessageBus;

        await EnsureConsumersStarted();

        // act

        // publish
        var stopwatch = Stopwatch.StartNew();

        var requests = Enumerable
            .Range(0, NumberOfMessages)
            .Select(i => new EchoRequest { Index = i, Message = $"Echo {i}" })
            .ToList();

        var responses = new List<(EchoRequest Request, EchoResponse Response)>();
        var responseTasks = requests.Select(async req =>
        {
            var resp = await messageBus.Send<EchoResponse, EchoRequest>(req).ConfigureAwait(false);
            lock (responses)
            {
                Logger.LogDebug("Recieved response for index {0:000}", req.Index);
                responses.Add((req, resp));
            }
        });
        await Task.WhenAll(responseTasks).ConfigureAwait(false);

        stopwatch.Stop();
        Logger.LogInformation("Published and received {0} messages in {1}", responses.Count, stopwatch.Elapsed);

        // assert

        // all messages got back
        responses.Count.Should().Be(NumberOfMessages);
        responses.All(x => x.Request.Message == x.Response.Message).Should().BeTrue();
    }

    private static async Task<IList<PingMessage>> ConsumeAll(PingConsumer consumer, int? expectedCount, int newMessagesAwaitingTimeout = 5)
    {
        var lastMessageCount = 0;
        var lastMessageStopwatch = Stopwatch.StartNew();

        while (lastMessageStopwatch.Elapsed.TotalSeconds < newMessagesAwaitingTimeout && (expectedCount == null || expectedCount.Value != consumer.Messages.Count))
        {
            await Task.Delay(100).ConfigureAwait(false);

            if (consumer.Messages.Count != lastMessageCount)
            {
                lastMessageCount = consumer.Messages.Count;
                lastMessageStopwatch.Restart();
            }
        }
        lastMessageStopwatch.Stop();
        return consumer.Messages;
    }

    private class PingMessage
    {
        public int Counter { get; set; }
        public Guid Value { get; set; }

        #region Overrides of Object

        public override string ToString() => $"PingMessage(Counter={Counter}, Value={Value})";

        #endregion
    }

    private class PingConsumer : IConsumer<PingMessage>, IConsumerWithContext
    {
        private readonly ILogger _logger;

        public PingConsumer(ILogger logger)
        {
            _logger = logger;
        }

        public IConsumerContext Context { get; set; }
        public IList<PingMessage> Messages { get; } = new List<PingMessage>();

        #region Implementation of IConsumer<in PingMessage>

        public Task OnHandle(PingMessage message)
        {
            lock (this)
            {
                Messages.Add(message);
            }

            _logger.LogInformation("Got message {0} on topic {1}.", message.Counter, Context.Path);
            return Task.CompletedTask;
        }

        #endregion
    }

    private class EchoRequest /*: IRequestMessage<EchoResponse>*/
    {
        public int Index { get; set; }
        public string Message { get; set; }

        #region Overrides of Object

        public override string ToString() => $"EchoRequest(Index={Index}, Message={Message})";

        #endregion
    }

    private class EchoResponse
    {
        public string Message { get; set; }

        #region Overrides of Object

        public override string ToString() => $"EchoResponse(Message={Message})";

        #endregion
    }

    private class EchoRequestHandler : IRequestHandler<EchoRequest, EchoResponse>
    {
        public Task<EchoResponse> OnHandle(EchoRequest request)
        {
            return Task.FromResult(new EchoResponse { Message = request.Message });
        }
    }
}