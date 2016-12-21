using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Logging;
using RdKafka;
using SlimMessageBus.Config;
using SlimMessageBus.Host;

namespace SlimMessageBus.Provider.Kafka
{
    /// <summary>
    ///
    /// Note the assumption is that Topic/Producer/Consumer are all thread-safe (see https://github.com/edenhill/librdkafka/issues/215)
    /// </summary>
    ///
    public class KafkaMessageBus : BaseMessageBus
    {
        private static readonly ILog Log = LogManager.GetLogger<KafkaMessageBus>();

        //private MessageBusConfiguration _configuration;
        private readonly KafkaMessageBusSettings _kafkaSettings;

        private Producer _producer;
        private readonly IDictionary<string, KafkaTopic> _topics = new Dictionary<string, KafkaTopic>();

        public KafkaMessageBus(MessageBusSettings settings, KafkaMessageBusSettings kafkaSettings)
            : base(settings)
        {
            _kafkaSettings = kafkaSettings;
            _producer = new Producer(kafkaSettings.BrokerList);

            foreach (var topicName in Settings.Publishers.Select(x => x.DefaultTopic).Distinct())
            {
                Log.DebugFormat("Creating Kafka topic {0}", topicName);
                _topics.Add(topicName, new KafkaTopic(topicName, _producer));
            }
        }

        #region Overrides of BaseMessageBus

        public override void Dispose()
        {
            foreach (var topic in _topics.Values)
            {
                topic.Dispose();
            }
            _topics.Clear();

            if (_producer != null)
            {
                _producer.Dispose();
                _producer = null;
            }

            base.Dispose();
        }

        protected override async Task Publish(Type type, string topic, byte[] payload, string replyTo = null)
        {
            // lookup the Kafka topic
            var kafkaTopic = _topics[topic];
            // send the message to topic
            var deliveryReport = await kafkaTopic.Topic.Produce(payload);
            // log some debug information
            Log.DebugFormat("Delivered message with offset {0} and partition {1}", deliveryReport.Offset, deliveryReport.Partition);
        }

        #endregion

        protected class KafkaTopic : IDisposable
        {
            public string Name;
            public Topic Topic;
            //public SemaphoreSlim Semaphore;

            public KafkaTopic(string name, Producer producer)
            {
                Name = name;
                Topic = producer.Topic(name);
                //Semaphore = new SemaphoreSlim(1);
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
}