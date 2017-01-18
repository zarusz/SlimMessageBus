using System;
using Common.Logging;
using RdKafka;

namespace SlimMessageBus.Provider.Kafka
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
                Topic.DisposeSilently(e => Log.WarnFormat("Error occured while disposing kafka topic. {0}", e));
                Topic = null;
            }
        }

        #endregion
    }
}