using System;
using RdKafka;

namespace SlimMessageBus.Provider.Kafka
{
    public class KafkaTopic : IDisposable
    {
        public string Name;
        public Topic Topic;

        public KafkaTopic(string name, Producer producer)
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