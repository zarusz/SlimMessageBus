using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using RdKafka;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Provider.Kafka
{
    /// <summary>
    ///
    /// Note the assumption is that Topic/Producer/Consumer are all thread-safe (see https://github.com/edenhill/librdkafka/issues/215)
    /// </summary>
    public class KafkaMessageBus : BaseMessageBus
    {
        private static readonly ILog Log = LogManager.GetLogger<KafkaMessageBus>();

        public KafkaMessageBusSettings KafkaSettings { get; }

        private Producer _producer;
        private readonly IDictionary<string, KafkaTopic> _topics = new Dictionary<string, KafkaTopic>();                                                     
        private readonly IList<KafkaGroupConsumer> _groupConsumers = new List<KafkaGroupConsumer>();

        public KafkaMessageBus(MessageBusSettings settings, KafkaMessageBusSettings kafkaSettings)
            : base(settings)
        {
            KafkaSettings = kafkaSettings;

            Log.Info("Creating producers");
            _producer = new Producer(kafkaSettings.BrokerList);
            foreach (var topicName in Settings.Publishers.Select(x => x.DefaultTopic).Distinct())
            {
                Log.DebugFormat("Creating Kafka topic {0}", topicName);
                _topics.Add(topicName, new KafkaTopic(topicName, _producer));
            }

            Log.Info("Creating subscribers");
            foreach (var subscribersByGroup in settings.Subscribers.GroupBy(x => x.Group))
            {
                var group = subscribersByGroup.Key;

                foreach (var subscribersByMessageType in subscribersByGroup.GroupBy(x => x.MessageType))
                {
                    var messageType = subscribersByMessageType.Key;

                    _groupConsumers.Add(new KafkaGroupConsumer(this, group, messageType, subscribersByMessageType.ToList()));
                }
            }
            
        }

        #region Overrides of BaseMessageBus

        public override void Dispose()
        {
            foreach (var groupConsumer in _groupConsumers)
            {
                groupConsumer.Dispose();
            }
            _groupConsumers.Clear();

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

        protected override async Task Publish(Type type, string topic, byte[] payload)
        {
            // lookup the Kafka topic
            var kafkaTopic = _topics[topic];
            // send the message to topic
            var deliveryReport = await kafkaTopic.Topic.Produce(payload);
            // log some debug information
            Log.DebugFormat("Delivered message with offset {0} and partition {1}", deliveryReport.Offset, deliveryReport.Partition);
        }

        #endregion
    }
}