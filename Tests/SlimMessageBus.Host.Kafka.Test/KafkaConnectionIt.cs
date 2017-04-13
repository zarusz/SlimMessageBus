using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.VisualStudio.TestTools.UnitTesting;


namespace SlimMessageBus.Host.Kafka.Test
{
    [TestClass]
    public class KafkaConnectionIt
    {
        private const string BrokerList = "127.0.0.1:9092";
        private const string TopicName = "testtopic";

        [TestMethod, TestCategory("Integration")]
        public async Task ProduceToTopic()
        {
            var config = new Dictionary<string, object>
            {
                { KafkaConfigKeys.Servers, BrokerList }
            };
            using (var producer = new Producer(config))
            {
                var data = Encoding.UTF8.GetBytes("Hello Kafka " + DateTime.Now.ToString("HH:mm:ss"));
                var deliveryReport = await producer.ProduceAsync(TopicName, null, data);

                Console.WriteLine($"Produced to Partition: {deliveryReport.Partition}, Offset: {deliveryReport.Offset}");
            }
        }

        [TestMethod, TestCategory("Integration")]
        public void ConsumeFromTopic()
        {
            var config = new Dictionary<string, object>
            {
                {KafkaConfigKeys.Servers, BrokerList},
                {KafkaConfigKeys.Consumer.GroupId, "simple-csharp-consumer"},
                {
                    "default.topic.config", new Dictionary<string, object>
                    {
                        {"auto.offset.reset", "smallest"}
                    }
                }
            };

            using (var consumer = new Consumer(config))
            {
                var numConsumed = 0;
                consumer.OnMessage += (obj, msg) =>
                {
                    var text = Encoding.UTF8.GetString(msg.Value, 0, msg.Value.Length);
                    Console.WriteLine($"Topic: {msg.Topic} Partition: {msg.Partition} Offset: {msg.Offset} {text}");
                    numConsumed++;
                };

                consumer.Subscribe(new List<string> { TopicName });

                Console.WriteLine("Started consumer...");

                Task.Factory.StartNew(() =>
                {
                    var start = DateTime.Now;
                    while (DateTime.Now.Subtract(start).TotalSeconds < 5000 && numConsumed == 0)
                    {
                        consumer.Poll(1000);
                    }
                }).Wait();
            }
        }
    }
}
