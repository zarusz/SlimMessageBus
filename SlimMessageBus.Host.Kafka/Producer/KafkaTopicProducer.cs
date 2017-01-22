using System;
using Common.Logging;
using RdKafka;

namespace SlimMessageBus.Host.Kafka
{
    public class KafkaTopicProducer : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger<KafkaTopicProducer>();

        public string Name;
        public Topic Topic;

        public KafkaTopicProducer(string name, Producer producer)
        {
            Name = name;
            Topic = producer.Topic(name);
        }

        #region Implementation of IDisposable

        public void Dispose()
        {
            if (Topic != null)
            {
                Topic.DisposeSilently("kafka topic", Log);
                Topic = null;
            }
        }

        #endregion
    }
}