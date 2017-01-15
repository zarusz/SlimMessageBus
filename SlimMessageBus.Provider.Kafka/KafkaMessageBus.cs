using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly IDictionary<string, KafkaTopicProducer> _topics = new Dictionary<string, KafkaTopicProducer>();                                                     
        private readonly IList<KafkaGroupConsumerBase> _groupConsumers = new List<KafkaGroupConsumerBase>();

        public KafkaMessageBus(MessageBusSettings settings, KafkaMessageBusSettings kafkaSettings)
            : base(settings)
        {
            KafkaSettings = kafkaSettings;

            Log.Info("Creating producers");
            _producer = new Producer(kafkaSettings.BrokerList);
            foreach (var topicName in Settings.Publishers.Select(x => x.DefaultTopic).Distinct())
            {
                Log.DebugFormat("Creating topic producer {0}", topicName);
                _topics.Add(topicName, new KafkaTopicProducer(topicName, _producer));
            }

            Log.Info("Creating subscribers");
            foreach (var subscribersByGroup in settings.Subscribers.GroupBy(x => x.Group))
            {
                var group = subscribersByGroup.Key;

                foreach (var subscribersByMessageType in subscribersByGroup.GroupBy(x => x.MessageType))
                {
                    var messageType = subscribersByMessageType.Key;

                    Log.InfoFormat("Creating consumer for topics {0}, group {1} and message type {2}", string.Join(",", subscribersByMessageType.Select(x => x.Topic)), group, messageType);
                    _groupConsumers.Add(new KafkaGroupConsumer(this, group, messageType, subscribersByMessageType.ToList()));
                }
            }

            if (settings.RequestResponse != null)
            {
                Log.InfoFormat("Creating response consumer for topic {0} and group {1}", settings.RequestResponse.Group, settings.RequestResponse.Topic);
                _groupConsumers.Add(new KafkaResponseConsumer(this, settings.RequestResponse));
            }
        }

        #region Overrides of BaseMessageBus

        public override void Dispose()
        {
            foreach (var groupConsumer in _groupConsumers)
            {
                try
                {
                    groupConsumer.Dispose();
                }
                catch (Exception e)
                {
                    Log.WarnFormat("Error occured while disposing consumer group {0}. {1}", groupConsumer.Group, e);
                }
            }
            _groupConsumers.Clear();

            foreach (var topic in _topics.Values)
            {
                try
                {
                    topic.Dispose();
                }
                catch (Exception e)
                {
                    Log.WarnFormat("Error occured while disposing topic {0}. {1}", topic.Name, e);
                }
            }
            _topics.Clear();

            if (_producer != null)
            {
                try
                {
                    _producer.Dispose();
                }
                catch (Exception e)
                {
                    Log.WarnFormat("Error occured while producer. {0}", e);
                }
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