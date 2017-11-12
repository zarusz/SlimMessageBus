using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using FluentAssertions;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.Serialization.Json;
using Xunit;
using System.Linq;

namespace SlimMessageBus.Host.Kafka.Test
{
    public class KafkaMessageBusIt : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger<KafkaMessageBusIt>();

        private const int NumberOfMessages = 77;

        private KafkaMessageBusSettings KafkaSettings { get; }
        private MessageBusBuilder MessageBusBuilder { get; }
        private Lazy<KafkaMessageBus> MessageBus { get; }

        public KafkaMessageBusIt()
        {
            // address to the Kafka broker
            var kafkaBrokers = "localhost:9092";

            KafkaSettings = new KafkaMessageBusSettings(kafkaBrokers)
            {
                ProducerConfigFactory = () => new Dictionary<string, object>
                {
                    {"socket.blocking.max.ms",1},
                    {"queue.buffering.max.ms",1},
                    {"socket.nagle.disable", true}
                },
                ConsumerConfigFactory = (group) => new Dictionary<string, object>
                {
                    {"socket.blocking.max.ms", 1},
                    {"fetch.error.backoff.ms", 1},
                    {"statistics.interval.ms", 500000},
                    {"socket.nagle.disable", true},
                    {"auto.offset.reset", "earliest"}
                }
            };

            MessageBusBuilder = new MessageBusBuilder()
                .WithSerializer(new JsonMessageSerializer())
                .WithProviderKafka(KafkaSettings);

            MessageBus = new Lazy<KafkaMessageBus>(() => (KafkaMessageBus)MessageBusBuilder.Build());
        }

        public void Dispose()
        {
            MessageBus.Value.Dispose();
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void BasicPubSub()
        {
            // arrange
            
            // ensure the topic has 2 partitions
            var topic = $"test-ping";

            var pingConsumer = new PingConsumer();

            MessageBusBuilder
                .Publish<PingMessage>(x =>
                {
                    x.DefaultTopic(topic);
                    // Partition #0 for even counters
                    // Partition #1 for odd counters
                    x.PartitionProvider((m, t) => m.Counter % 2);
                })
                .SubscribeTo<PingMessage>(x => x.Topic(topic)
                                                .Group("subscriber")
                                                .WithSubscriber<PingConsumer>()
                                                .Instances(2))
                .WithDependencyResolver(new LookupDependencyResolver(f =>
                {
                    if (f == typeof(PingConsumer)) return pingConsumer;
                    throw new InvalidOperationException();
                }));                                                

            var kafkaMessageBus = MessageBus.Value;

            // act

            // publish
            var stopwatch = Stopwatch.StartNew();

            var messages = Enumerable.Range(0, NumberOfMessages).Select(i => new PingMessage { Counter = i, Timestamp = DateTime.UtcNow }).ToList();
            messages.AsParallel().ForAll(m => MessageBus.Value.Publish(m).Wait());

            stopwatch.Stop();
            Log.InfoFormat("Published {0} messages in {1}", messages.Count, stopwatch.Elapsed);

            // consume
            stopwatch.Restart();
            var messagesReceived = ConsumeFromTopic(pingConsumer);
            stopwatch.Stop();
            Log.InfoFormat("Consumed {0} messages in {1}", messagesReceived.Count, stopwatch.Elapsed);

            // assert

            // all messages got back
            messagesReceived.Count.ShouldBeEquivalentTo(messages.Count);

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
        [Trait("Category", "Integration")]
        public void BasicReqResp()
        {
            // arrange

            // ensure the topic has 2 partitions
            var topic = $"test-echo";
            var echoRequestHandler = new EchoRequestHandler();

            MessageBusBuilder
                .Publish<EchoRequest>(x =>
                {
                    x.DefaultTopic(topic);
                    // Partition #0 for even indices
                    // Partition #1 for odd indices
                    x.PartitionProvider((m, t) => m.Index % 2);
                })
                .Handle<EchoRequest, EchoResponse>(x => x.Topic(topic)
                                                         .Group("handler")
                                                         .WithHandler<EchoRequestHandler>()
                                                         .Instances(2))
                .ExpectRequestResponses(x =>
                {
                    x.ReplyToTopic("test-echo-resp");
                    x.Group("response-reader");
                    x.DefaultTimeout(TimeSpan.FromSeconds(30));
                })
                .WithDependencyResolver(new LookupDependencyResolver(f =>
                {
                    if (f == typeof(EchoRequestHandler)) return echoRequestHandler;
                    throw new InvalidOperationException();
                }));

            var kafkaMessageBus = MessageBus.Value;

            // act

            var requests = Enumerable.Range(0, NumberOfMessages).Select(i => new EchoRequest { Index = i, Message = $"Echo {i}" }).ToList();
            var responses = new List<Tuple<EchoRequest, EchoResponse>>();
            requests.AsParallel().ForAll(req =>
            {
                var resp = MessageBus.Value.Send(req).Result;
                lock (responses)
                {
                    responses.Add(Tuple.Create(req, resp));
                }
            });

            // assert

            // all messages got back
            responses.Count.ShouldBeEquivalentTo(NumberOfMessages);
            responses.All(x => x.Item1.Message == x.Item2.Message).Should().BeTrue();
        }

        private IList<Tuple<PingMessage, int>> ConsumeFromTopic(PingConsumer pingConsumer)
        {
            var lastMessageCount = 0;
            var lastMessageStopwatch = Stopwatch.StartNew();

            const int NewMessagesAwatingTimeout = 10;

            while (lastMessageStopwatch.Elapsed.TotalSeconds < NewMessagesAwatingTimeout)
            {
                Thread.Sleep(200);

                if (pingConsumer.Messages.Count != lastMessageCount)
                {
                    lastMessageCount = pingConsumer.Messages.Count;
                    lastMessageStopwatch.Restart();
                }
            }
            lastMessageStopwatch.Stop();
            return pingConsumer.Messages;
        }

        public class PingMessage
        {
            public DateTime Timestamp { get; set; }
            public int Counter { get; set; }
        }

        public class PingConsumer : IConsumer<PingMessage>, IConsumerContextAware
        {
            private static readonly ILog Log = LogManager.GetLogger<PingConsumer>();

            public AsyncLocal<ConsumerContext> Context { get; } = new AsyncLocal<ConsumerContext>();
            public IList<Tuple<PingMessage, int>> Messages { get; } = new List<Tuple<PingMessage, int>>();

            #region Implementation of IConsumer<in PingMessage>

            public Task OnHandle(PingMessage message, string topic)
            {
                lock (this)
                {
                    var transportMessage = Context.Value.GetTransportMessage();
                    var partition = transportMessage.TopicPartition.Partition;

                    Messages.Add(Tuple.Create(message, partition));
                }

                Log.InfoFormat("Got message {0} on topic {1}.", message.Counter, topic);
                return Task.CompletedTask;
            }

            #endregion
        }

        public class EchoRequest: IRequestMessage<EchoResponse>
        {
            public int Index { get; set; }
            public string Message { get; set; }
        }

        public class EchoResponse
        {
            public string Message { get; set; }
        }

        public class EchoRequestHandler : IRequestHandler<EchoRequest, EchoResponse>
        {
            public Task<EchoResponse> OnHandle(EchoRequest request, string topic)
            {
                return Task.FromResult(new EchoResponse { Message = request.Message });
            }
        }
    }
}