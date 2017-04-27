using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.Serialization.Json;

namespace SlimMessageBus.Host.Kafka.Test
{
    public class PingMessage
    {
        public DateTime Timestamp { get; set; }
        public int Counter { get; set; }
    }

    public class PingConsumer : IConsumer<PingMessage>
    {
        private static readonly ILog Log = LogManager.GetLogger<PingConsumer>();

        public IList<PingMessage> Messages { get; }

        public PingConsumer()
        {
            Messages = new List<PingMessage>();
        }

        #region Implementation of ISubscriber<in PingMessage>

        public Task OnHandle(PingMessage message, string topic)
        {
            lock (Messages)
            {
                Messages.Add(message);
            }

            Log.InfoFormat("Got message {0} on topic {1}.", message.Counter, topic);
            return Task.FromResult(false);
        }

        #endregion
    }


    [TestClass]
    public class KafkaMessageBusIt
    {
        private static readonly ILog Log = LogManager.GetLogger<KafkaMessageBusIt>();

        private const int NumberOfMessages = 77;
        private KafkaMessageBus _bus;
        private PingConsumer _pingConsumer;

        private class FakeDependencyResolver : IDependencyResolver
        {
            private readonly PingConsumer _pingConsumer;

            public FakeDependencyResolver(PingConsumer pingConsumer)
            {
                _pingConsumer = pingConsumer;
            }

            #region Implementation of IDependencyResolver

            public object Resolve(Type type)
            {
                if (type.IsAssignableFrom(typeof(PingConsumer)))
                {
                    return _pingConsumer;
                }
                return null;
            }

            #endregion
        }

        [TestInitialize]
        public void SetupBus()
        {
            _pingConsumer = new PingConsumer();

            //var testTopic = $"test-ping-{DateTime.Now.Ticks}";
            var topic = $"test-ping";

            // some unique string across all application instances
            var instanceId = "1";
            // address to your Kafka broker
            var kafkaBrokers = "127.0.0.1:9092";

            var messageBusBuilder = new MessageBusBuilder()
                .Publish<PingMessage>(x =>
                {
                    x.DefaultTopic(topic);
                })
                .SubscribeTo<PingMessage>(x =>
                {
                    x.Topic(topic)
                        .Group("subscriber2")
                        .WithSubscriber<PingConsumer>()
                        .Instances(2);
                })
                .ExpectRequestResponses(x =>
                {
                    x.ReplyToTopic($"worker-{instanceId}-response");
                    x.Group($"worker-{instanceId}");
                    x.DefaultTimeout(TimeSpan.FromSeconds(10));
                })
                .WithSerializer(new JsonMessageSerializer())
                .WithDependencyResolver(new FakeDependencyResolver(_pingConsumer))
                .WithProviderKafka(new KafkaMessageBusSettings(kafkaBrokers)
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
                        {"socket.nagle.disable", true}
                    }
                });

            _bus = (KafkaMessageBus)messageBusBuilder.Build();
        }

        [TestCleanup]
        public void DisposeBus()
        {
            _bus.Dispose();
        }

        [TestMethod, TestCategory("Integration")]
        public void BasicIntegration()
        {
            // ensure the subscribers have established connections and are ready
            Thread.Sleep(2000);

            var stopwatch = new Stopwatch();

            stopwatch.Start();
            var messagesPublished = PublishToTopic();

            stopwatch.Stop();
            Log.InfoFormat("Published {0} messages in {1}", messagesPublished.Count, stopwatch.Elapsed);

            stopwatch.Restart();
                                                   
            var messagesReceived = ConsumeFromTopic();

            messagesReceived.Count.ShouldBeEquivalentTo(messagesPublished.Count);

            stopwatch.Stop();
            Log.InfoFormat("Consumed {0} messages in {1}", messagesReceived.Count, stopwatch.Elapsed);            
        }

        private IList<PingMessage> ConsumeFromTopic()
        {
            while (_pingConsumer.Messages.Count < NumberOfMessages)
            {
                Thread.Sleep(200);
            }
            return _pingConsumer.Messages;
        }

        private IList<PingMessage> PublishToTopic()
        {
            var messages = new List<PingMessage>();
            for (var i = 0; i < NumberOfMessages; i++)
            {
                messages.Add(new PingMessage()
                {
                    Counter = i,
                    Timestamp = DateTime.UtcNow
                });
            }

            Parallel.ForEach(messages, m =>
            {
                try
                {
                    _bus.Publish(m).Wait();
                }
                catch (Exception e)
                {
                    Log.ErrorFormat("Could not publish: {0}", e);                    
                }

            });

            return messages;
        }

    }
}