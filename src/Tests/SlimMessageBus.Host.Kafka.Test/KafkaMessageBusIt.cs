using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using FluentAssertions;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.Serialization.Json;
using Xunit;
using System.Linq;
using System.Reflection;
using Common.Logging.Simple;
using Microsoft.Extensions.Configuration;
using SlimMessageBus.Host.DependencyResolver;
using SlimMessageBus.Host.Kafka.Configs;
using SecretStore;
using System.Collections.Concurrent;

namespace SlimMessageBus.Host.Kafka.Test
{
    /// <summary>
    /// Performs basic integration test to verify that pub/sub and request-response communication works while concurrent producers pump data.
    /// <remarks>
    /// Ensure the topics used in this test (test-ping and test-echo) have 2 partitions, otherwise you will get an exception (Confluent.Kafka.KafkaException : Local: Unknown partition)
    /// See https://kafka.apache.org/quickstart#quickstart_createtopic
    /// <code>bin\windows\kafka-topics.bat --create --zookeeper localhost:2181 --partitions 2 --replication-factor 1 --topic test-ping</code>
    /// <code>bin\windows\kafka-topics.bat --create --zookeeper localhost:2181 --partitions 2 --replication-factor 1 --topic test-echo</code>
    /// </remarks>
    /// </summary>
    [Trait("Category", "Integration")]
    [Trait("Category", "Local")]
    public class KafkaMessageBusIt : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private const int NumberOfMessages = 77;

        private KafkaMessageBusSettings KafkaSettings { get; }
        private MessageBusBuilder MessageBusBuilder { get; }
        private Lazy<KafkaMessageBus> MessageBus { get; }

        public KafkaMessageBusIt()
        {
            LogManager.Adapter = new DebugLoggerFactoryAdapter();
            //LogManager.Adapter = new NoOpLoggerFactoryAdapter();

            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            Secrets.Load(@"..\..\..\..\..\secrets.txt");

            var kafkaBrokers = configuration["Kafka:Brokers"];
            var kafkaUsername = Secrets.Service.PopulateSecrets(configuration["Kafka:Username"]);
            var kafkaPassword = Secrets.Service.PopulateSecrets(configuration["Kafka:Password"]);

            KafkaSettings = new KafkaMessageBusSettings(kafkaBrokers)
            {
                ProducerConfigFactory = () => new Dictionary<string, object>
                {
                    {"socket.blocking.max.ms", 1},
                    {"queue.buffering.max.ms", 1},
                    {"socket.nagle.disable", true},
                    //{"request.required.acks", 0}
                },
                ConsumerConfigFactory = (group) => new Dictionary<string, object>
                {
                    {"socket.blocking.max.ms", 1},
                    {"fetch.error.backoff.ms", 1},
                    {"statistics.interval.ms", 500000},
                    {"socket.nagle.disable", true},
                    {KafkaConfigKeys.ConsumerKeys.AutoOffsetReset, KafkaConfigValues.AutoOffsetReset.Earliest}
                }
            };
            MessageBusBuilder = MessageBusBuilder.Create()
                .WithSerializer(new JsonMessageSerializer())
                .WithProviderKafka(KafkaSettings);

            MessageBus = new Lazy<KafkaMessageBus>(() => (KafkaMessageBus)MessageBusBuilder.Build());
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                MessageBus.Value.Dispose();
            }
        }

        [Fact]
        public async Task BasicPubSub()
        {
            // arrange

            // ensure the topic has 2 partitions
            var topic = "test-ping";

            var pingConsumer = new PingConsumer();

            MessageBusBuilder
                .Produce<PingMessage>(x =>
                {
                    x.DefaultTopic(topic);
                    // Partition #0 for even counters
                    // Partition #1 for odd counters
                    x.PartitionProvider((m, t) => m.Counter % 2);
                })
                .Consume<PingMessage>(x => {
                    x.Topic(topic)
                        .WithConsumer<PingConsumer>()
                        .Group("subscriber")
                        .Instances(2)
                        .CheckpointEvery(1000)
                        .CheckpointAfter(TimeSpan.FromSeconds(600));
                })
                .WithDependencyResolver(new LookupDependencyResolver(f =>
                {
                    if (f == typeof(PingConsumer)) return pingConsumer;
                    throw new InvalidOperationException();
                }));

            var messageBus = MessageBus.Value;

            // act

            // publish
            var stopwatch = Stopwatch.StartNew();

            var messages = Enumerable
                .Range(0, NumberOfMessages)
                .Select(i => new PingMessage { Counter = i, Timestamp = DateTime.UtcNow })
                .ToList();

            await Task.WhenAll(messages.Select(m => messageBus.Publish(m)));

            stopwatch.Stop();
            Log.InfoFormat(CultureInfo.InvariantCulture, "Published {0} messages in {1}", messages.Count, stopwatch.Elapsed);

            // consume
            stopwatch.Restart();
            
            await WaitWhileMessagesAreFlowing(() => pingConsumer.Messages.Count);
            var messagesReceived = pingConsumer.Messages;

            stopwatch.Stop();
            Log.InfoFormat(CultureInfo.InvariantCulture, "Consumed {0} messages in {1}", messagesReceived.Count, stopwatch.Elapsed);

            // assert

            // all messages got back
            messagesReceived.Count.Should().Be(messages.Count);

            // Partition #0 => Messages with even counter
            messagesReceived
                .Where(x => x.Item2 == 0)
                .All(x => x.Item1.Counter % 2 == 0)
                .Should().BeTrue();

            // Partition #1 => Messages with odd counter
            messagesReceived
                .Where(x => x.Item2 == 1)
                .All(x => x.Item1.Counter % 2 == 1)
                .Should().BeTrue();
        }

        [Fact]
        public async Task BasicReqResp()
        {
            // arrange

            // ensure the topic has 2 partitions
            var topic = "test-echo";
            var echoRequestHandler = new EchoRequestHandler();

            MessageBusBuilder
                .Produce<EchoRequest>(x =>
                {
                    x.DefaultTopic(topic);
                    // Partition #0 for even indices
                    // Partition #1 for odd indices
                    x.PartitionProvider((m, t) => m.Index % 2);
                })
                .Handle<EchoRequest, EchoResponse>(x => x.Topic(topic)
                                                         .WithHandler<EchoRequestHandler>()
                                                         .Group("handler")
                                                         .Instances(2)
                                                         .CheckpointEvery(1000)
                                                         .CheckpointAfter(TimeSpan.FromSeconds(60)))
                .ExpectRequestResponses(x =>
                {
                    x.ReplyToTopic("test-echo-resp");
                    x.Group("response-reader");
                    // for subsequent test runs allow enough time for kafka to reassign the partitions
                    x.DefaultTimeout(TimeSpan.FromSeconds(60));
                    x.CheckpointEvery(100);
                    x.CheckpointAfter(TimeSpan.FromSeconds(10));
                })
                .WithDependencyResolver(new LookupDependencyResolver(f =>
                {
                    if (f == typeof(EchoRequestHandler)) return echoRequestHandler;
                    throw new InvalidOperationException();
                }));

            var kafkaMessageBus = MessageBus.Value;

            // act

            var requests = Enumerable
                .Range(0, NumberOfMessages)
                .Select(i => new EchoRequest { Index = i, Message = $"Echo {i}" })
                .ToList();

            var responses = new ConcurrentBag<ValueTuple<EchoRequest, EchoResponse>>();
            await Task.WhenAll(requests.Select(async req =>
            {
                var resp = await kafkaMessageBus.Send(req);
                responses.Add((req, resp));
            }));

            await WaitWhileMessagesAreFlowing(() => responses.Count);

            // assert

            // all messages got back
            responses.Count.Should().Be(NumberOfMessages);
            responses.All(x => x.Item1.Message == x.Item2.Message).Should().BeTrue();
        }

        private static async Task WaitWhileMessagesAreFlowing(Func<int> counterFunc)
        {
            var lastMessageCount = 0;
            var lastMessageStopwatch = Stopwatch.StartNew();

            const int newMessagesAwaitingTimeout = 10;

            while (lastMessageStopwatch.Elapsed.TotalSeconds < newMessagesAwaitingTimeout)
            {
                await Task.Delay(200);

                if (counterFunc() != lastMessageCount)
                {
                    lastMessageCount = counterFunc();
                    lastMessageStopwatch.Restart();
                }
            }
            lastMessageStopwatch.Stop();
        }

        private class PingMessage
        {
            public DateTime Timestamp { get; set; }
            public int Counter { get; set; }

            #region Overrides of Object

            public override string ToString() => $"PingMessage(Counter={Counter}, Timestamp={Timestamp})";

            #endregion
        }

        private class PingConsumer : IConsumer<PingMessage>, IConsumerContextAware
        {
            // ReSharper disable once MemberHidesStaticFromOuterClass
            private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

            public AsyncLocal<ConsumerContext> Context { get; } = new AsyncLocal<ConsumerContext>();
            public ConcurrentBag<ValueTuple<PingMessage, int>> Messages { get; } = new ConcurrentBag<ValueTuple<PingMessage, int>>();

            #region Implementation of IConsumer<in PingMessage>

            public Task OnHandle(PingMessage message, string name)
            {
                var transportMessage = Context.Value.GetTransportMessage();
                var partition = transportMessage.TopicPartition.Partition;

                Messages.Add((message, partition));

                Log.InfoFormat(CultureInfo.InvariantCulture, "Got message {0} on topic {1}.", message.Counter, name);
                return Task.CompletedTask;
            }

            #endregion
        }

        private class EchoRequest : IRequestMessage<EchoResponse>
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
            public Task<EchoResponse> OnHandle(EchoRequest request, string name)
            {
                return Task.FromResult(new EchoResponse { Message = request.Message });
            }
        }
    }
}