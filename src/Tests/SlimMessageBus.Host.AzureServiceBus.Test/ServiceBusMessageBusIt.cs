namespace SlimMessageBus.Host.AzureServiceBus.Test
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using SecretStore;
    using SlimMessageBus.Host.Test.Common;
    using SlimMessageBus.Host.Config;
    using SlimMessageBus.Host.DependencyResolver;
    using SlimMessageBus.Host.Serialization.Json;
    using Xunit;
    using Xunit.Abstractions;
    using Azure.Messaging.ServiceBus;

    [Trait("Category", "Integration")]
    public class ServiceBusMessageBusIt : IDisposable
    {
        private const int NumberOfMessages = 77;

        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;

        private ServiceBusMessageBusSettings Settings { get; }
        private MessageBusBuilder MessageBusBuilder { get; }
        private Lazy<ServiceBusMessageBus> MessageBus { get; }

        public ServiceBusMessageBusIt(ITestOutputHelper testOutputHelper)
        {
            _loggerFactory = new XunitLoggerFactory(testOutputHelper);
            _logger = _loggerFactory.CreateLogger<ServiceBusMessageBusIt>();

            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            Secrets.Load(@"..\..\..\..\..\secrets.txt");

            var connectionString = Secrets.Service.PopulateSecrets(configuration["Azure:ServiceBus"]);

            Settings = new ServiceBusMessageBusSettings(connectionString);

            MessageBusBuilder = MessageBusBuilder.Create()
                .WithLoggerFacory(_loggerFactory)
                .WithSerializer(new JsonMessageSerializer())
                .WithProviderServiceBus(Settings);

            MessageBus = new Lazy<ServiceBusMessageBus>(() => (ServiceBusMessageBus)MessageBusBuilder.Build());
        }

        public void Dispose()
        {
            MessageBus.Value.Dispose();
            GC.SuppressFinalize(this);
        }

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

            MessageBusBuilder
                .Produce<PingMessage>(x => x.DefaultTopic(topic).WithModifier(MessageModifier))
                .Do(builder => Enumerable.Range(0, subscribers).ToList().ForEach(i =>
                {
                    builder.Consume<PingMessage>(x => x
                        .Topic(topic)
                        .SubscriptionName($"subscriber-{i}") // ensure subscription exists on the ServiceBus topic
                        .WithConsumer<PingConsumer>()
                        .WithConsumer<PingDerivedConsumer, PingDerivedMessage>()
                        .Instances(concurrency));
                }));

            await BasicPubSub(concurrency, subscribers, subscribers).ConfigureAwait(false);
        }

        [Fact]
        public async Task BasicPubSubOnQueue()
        {
            var concurrency = 2;
            var queue = "test-ping-queue";

            MessageBusBuilder
                .Produce<PingMessage>(x => x.DefaultQueue(queue).WithModifier(MessageModifier))
                .Consume<PingMessage>(x => x
                        .Queue(queue)
                        .WithConsumer<PingConsumer>()
                        .WithConsumer<PingDerivedConsumer, PingDerivedMessage>()
                        .Instances(concurrency));

            await BasicPubSub(concurrency, 1, 1).ConfigureAwait(false);
        }

        private static string GetMessageId(PingMessage message) => $"ID_{message.Counter}";

        public class TestData
        {
            public List<PingMessage> ProducedMessages { get; set; }
            public List<(PingMessage Message, string MessageId, string SessionId)> ConsumedMessages { get; set; }
        }

        private async Task BasicPubSub(int concurrency, int subscribers, int expectedMessageCopies, Action<TestData> additionalAssertion = null)
        {
            // arrange
            var consumersCreated = 0;
            var consumedMessages = new List<(PingMessage Message, string MessageId, string SessionId)>();

            MessageBusBuilder
                .WithDependencyResolver(new LookupDependencyResolver(f =>
                {
                    if (f == typeof(PingConsumer))
                    {
                        var pingConsumer = new PingConsumer(_loggerFactory.CreateLogger<PingConsumer>(), consumedMessages);
                        Interlocked.Increment(ref consumersCreated);
                        return pingConsumer;
                    }
                    if (f == typeof(PingDerivedConsumer))
                    {
                        var pingConsumer = new PingDerivedConsumer(_loggerFactory.CreateLogger<PingDerivedConsumer>(), consumedMessages);
                        Interlocked.Increment(ref consumersCreated);
                        return pingConsumer;
                    }

                    throw new InvalidOperationException();
                }));

            var messageBus = MessageBus.Value;

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
            _logger.LogInformation("Published {0} messages in {1}", producedMessages.Count, stopwatch.Elapsed);

            // consume
            stopwatch.Restart();

            await WaitUntilArriving(consumedMessages);

            stopwatch.Stop();

            // assert

            // ensure number of instances of consumers created matches
            consumersCreated.Should().Be(producedMessages.Count * expectedMessageCopies);
            consumedMessages.Count.Should().Be(producedMessages.Count * expectedMessageCopies);

            // ... the content should match
            foreach (var producedMessage in producedMessages)
            {
                var messageCopies = consumedMessages.Count(x => x.Message.Counter == producedMessage.Counter && x.Message.Value == producedMessage.Value && x.MessageId == GetMessageId(x.Message));
                messageCopies.Should().Be(expectedMessageCopies);
            }

            additionalAssertion?.Invoke(new TestData { ProducedMessages = producedMessages, ConsumedMessages = consumedMessages });
        }

        [Fact]
        public async Task BasicReqRespOnTopic()
        {
            var topic = "test-echo";

            MessageBusBuilder
                .Produce<EchoRequest>(x =>
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

            await BasicReqResp().ConfigureAwait(false);
        }

        [Fact]
        public async Task BasicReqRespOnQueue()
        {
            var queue = "test-echo-queue";

            MessageBusBuilder
                .Produce<EchoRequest>(x =>
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

            await BasicReqResp().ConfigureAwait(false);
        }

        private async Task BasicReqResp()
        {
            // arrange
            var consumersCreated = new ConcurrentBag<EchoRequestHandler>();

            MessageBusBuilder
                .WithDependencyResolver(new LookupDependencyResolver(f =>
                {
                    if (f != typeof(EchoRequestHandler)) throw new InvalidOperationException();
                    var consumer = new EchoRequestHandler();
                    consumersCreated.Add(consumer);
                    return consumer;
                }));

            var messageBus = MessageBus.Value;

            // act

            // publish
            var stopwatch = Stopwatch.StartNew();

            var requests = Enumerable
                .Range(0, NumberOfMessages)
                .Select(i => new EchoRequest { Index = i, Message = $"Echo {i}" })
                .ToList();

            var responses = new List<Tuple<EchoRequest, EchoResponse>>();
            var responseTasks = requests.Select(async req =>
            {
                var resp = await messageBus.Send(req).ConfigureAwait(false);
                lock (responses)
                {
                    responses.Add(Tuple.Create(req, resp));
                }
            });
            await Task.WhenAll(responseTasks).ConfigureAwait(false);

            stopwatch.Stop();
            _logger.LogInformation("Published and received {0} messages in {1}", responses.Count, stopwatch.Elapsed);

            // assert

            // all messages got back
            responses.Count.Should().Be(NumberOfMessages);
            responses.All(x => x.Item1.Message == x.Item2.Message).Should().BeTrue();
        }

        private static async Task WaitUntilArriving(IList<(PingMessage Message, string MessageId, string SessionId)> arrivedMessages)
        {
            var lastMessageCount = 0;
            var lastMessageStopwatch = Stopwatch.StartNew();

            const int newMessagesAwaitingTimeout = 10;

            while (lastMessageStopwatch.Elapsed.TotalSeconds < newMessagesAwaitingTimeout)
            {
                await Task.Delay(100).ConfigureAwait(false);

                if (arrivedMessages.Count != lastMessageCount)
                {
                    lastMessageCount = arrivedMessages.Count;
                    lastMessageStopwatch.Restart();
                }
            }
            lastMessageStopwatch.Stop();
        }

        [Fact]
        public async Task FIFOUsingSessionsOnQueue()
        {
            var concurrency = 1;
            var queue = "test-session-queue";

            MessageBusBuilder
                .Produce<PingMessage>(x => x.DefaultQueue(queue).WithModifier(MessageModifierWithSession))
                .Consume<PingMessage>(x => x
                        .Queue(queue)
                        .WithConsumer<PingConsumer>()
                        .WithConsumer<PingDerivedConsumer, PingDerivedMessage>()
                        .Instances(concurrency)
                        .EnableSession(x => x.MaxConcurrentSessions(10).SessionIdleTimeout(TimeSpan.FromSeconds(5))));

            await BasicPubSub(concurrency, 1, 1, CheckMessagesWithinSameSessionAreInOrder).ConfigureAwait(false);
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

            MessageBusBuilder
                .Produce<PingMessage>(x => x.DefaultTopic(queue).WithModifier(MessageModifierWithSession))
                .Consume<PingMessage>(x => x
                        .Topic(queue)
                        .WithConsumer<PingConsumer>()
                        .WithConsumer<PingDerivedConsumer, PingDerivedMessage>()
                        .Instances(concurrency)
                        .SubscriptionName($"subscriber") // ensure subscription exists on the ServiceBus topic
                        .EnableSession(x => x.MaxConcurrentSessions(10).SessionIdleTimeout(TimeSpan.FromSeconds(5))));

            await BasicPubSub(concurrency, 1, 1, CheckMessagesWithinSameSessionAreInOrder).ConfigureAwait(false);
        }
    }

    public class PingMessage
    {
        public int Counter { get; set; }
        public Guid Value { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public override string ToString() => $"PingMessage(Counter={Counter}, Value={Value})";
    }

    public class PingDerivedMessage : PingMessage
    {
        public override string ToString() => $"PingDerivedMessage(Counter={Counter}, Value={Value})";
    }

    public class PingConsumer : IConsumer<PingMessage>, IConsumerWithContext
    {
        private readonly ILogger _logger;

        public PingConsumer(ILogger logger, IList<(PingMessage, string, string)> messages)
        {
            _logger = logger;
            Messages = messages;
        }

        public IConsumerContext Context { get; set; }

        public IList<(PingMessage Message, string MessageId, string SessionId)> Messages { get; }

        #region Implementation of IConsumer<in PingMessage>

        public Task OnHandle(PingMessage message, string path)
        {
            lock (Messages)
            {
                var sbMessage = Context.GetTransportMessage();

                Messages.Add((message, sbMessage.MessageId, sbMessage.SessionId));
            }

            _logger.LogInformation("Got message {Counter:000} on path {Path}.", message.Counter, path);
            return Task.CompletedTask;
        }

        #endregion
    }

    public class PingDerivedConsumer : IConsumer<PingDerivedMessage>, IConsumerWithContext
    {
        private readonly ILogger _logger;

        public PingDerivedConsumer(ILogger logger, IList<(PingMessage, string, string)> messages)
        {
            _logger = logger;
            Messages = messages;
        }

        public IConsumerContext Context { get; set; }

        public IList<(PingMessage Message, string MessageId, string SessionId)> Messages { get; }

        #region Implementation of IConsumer<in PingMessage>

        public Task OnHandle(PingDerivedMessage message, string path)
        {
            lock (Messages)
            {
                var sbMessage = Context.GetTransportMessage();

                Messages.Add((message, sbMessage.MessageId, sbMessage.SessionId));
            }

            _logger.LogInformation("Got message {Counter:000} on path {Path}.", message.Counter, path);
            return Task.CompletedTask;
        }

        #endregion
    }

    public class EchoRequest : IRequestMessage<EchoResponse>
    {
        public int Index { get; set; }
        public string Message { get; set; }

        #region Overrides of Object

        public override string ToString() => $"EchoRequest(Index={Index}, Message={Message})";

        #endregion
    }

    public class EchoResponse
    {
        public string Message { get; set; }

        #region Overrides of Object

        public override string ToString() => $"EchoResponse(Message={Message})";

        #endregion
    }

    public class EchoRequestHandler : IRequestHandler<EchoRequest, EchoResponse>
    {
        public Task<EchoResponse> OnHandle(EchoRequest request, string name)
        {
            return Task.FromResult(new EchoResponse { Message = request.Message });
        }
    }
}