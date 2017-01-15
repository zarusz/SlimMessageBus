using System;
using RdKafka;

namespace SlimMessageBus.Provider.Kafka
{
    public class KafkaTopicProducer : IDisposable
    {
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
                Topic.Dispose();
                Topic = null;
            }
        }

        #endregion
    }
}