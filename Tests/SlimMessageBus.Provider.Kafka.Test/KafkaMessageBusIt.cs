using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SlimMessageBus.Config;
using SlimMessageBus.Host.Serialization;
using SlimMessageBus.Host.Serialization.Json;

namespace SlimMessageBus.Provider.Kafka.Test
{
    public class PingMessage
    {
        public DateTime Timestamp { get; set; }
        public int Counter { get; set; }
    }


    [TestClass]
    public class KafkaMessageBusIt
    {
        private KafkaMessageBus _bus;

        [TestInitialize]
        public void SetupBus()
        {
            // some unique string across all application instances
            var instanceId = "1";
            // address to your Kafka broker
            var kafkaBrokers = "127.0.0.1:9092";

            var messageBusBuilder = new MessageBusBuilder()
                .Publish<PingMessage>(x =>
                {
                    x.OnTopicByDefault("test-ping");
                })
                .SubscribeTo<PingMessage>(x =>
                {
                    x.OnTopic("test-ping");
                    //s.WithGroup("workers").Of(3);
                })
                .ExpectRequestResponses(x =>
                {
                    x.OnTopic($"worker-{instanceId}-response");
                    x.DefaultTimeout(TimeSpan.FromSeconds(10));
                })
                .WithSerializer(new JsonMessageSerializer())
                .WithProviderKafka(new KafkaMessageBusSettings(kafkaBrokers));

            _bus = (KafkaMessageBus) messageBusBuilder.Build();
        }

        [TestCleanup]
        public void DisposeBus()
        {
            _bus.Dispose();
        }

        [TestMethod]
        public void PublishToTopic()
        {
            var messages = new List<PingMessage>();
            for (var i = 0; i < 1000; i++)
            {
                messages.Add(new PingMessage()
                {
                    Counter = i,
                    Timestamp = DateTime.UtcNow
                });
            }

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            Parallel.ForEach(messages, (message) => _bus.Publish(message).Wait());

            stopwatch.Stop();
            Console.WriteLine("Sent {0} messages in {1}", messages.Count, stopwatch.Elapsed);
        }

    }
}