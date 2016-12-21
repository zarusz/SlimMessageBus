using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RdKafka;

namespace SlimMessageBus.Provider.Kafka.Test
{
    [TestClass]
    public class KafkaConnectionIt
    {
        private string _brokerList = "127.0.0.1:9092";

        [TestMethod]
        public async Task PublishToTopic()
        {
            var config = new RdKafka.Config();


            //using (var producer = new Producer(config, brokerList))
            using (var producer = new Producer(_brokerList))
            {
                using (var topic = producer.Topic("testtopic"))
                {
                    byte[] data = Encoding.UTF8.GetBytes("Hello RdKafka " + DateTime.Now.ToString("HH:mm:ss"));
                    DeliveryReport deliveryReport = await topic.Produce(data);

                    Console.WriteLine($"Produced to Partition: {deliveryReport.Partition}, Offset: {deliveryReport.Offset}");
                }
                Console.WriteLine($"test");
            }
        }

        [TestMethod]
        public async Task ConsumeFromTopic()
        {
            var config = new RdKafka.Config() { GroupId = "example-csharp-consumer" };
            using (var consumer = new EventConsumer(config, _brokerList))
            {
                consumer.OnMessage += (obj, msg) =>
                {
                    var text = Encoding.UTF8.GetString(msg.Payload, 0, msg.Payload.Length);
                    Console.WriteLine($"Topic: {msg.Topic} Partition: {msg.Partition} Offset: {msg.Offset} {text}");
                };

                consumer.Subscribe(new List<string>{ "testtopic" });
                consumer.Start();

                Console.WriteLine("Started consumer, press enter to stop consuming");
                //Console.ReadLine();

                Thread.Sleep(5000);
            }
        }
    }
}
