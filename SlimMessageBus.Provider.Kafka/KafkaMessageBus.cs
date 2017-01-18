using System;
using System.Collections.Concurrent;
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
    public class KafkaMessageBus : MessageBusBase
    {
        private static readonly ILog Log = LogManager.GetLogger<KafkaMessageBus>();

        public KafkaMessageBusSettings KafkaSettings { get; }

        private Producer _producer;
        private readonly ConcurrentDictionary<string, KafkaTopicProducer> _topicProducers = new ConcurrentDictionary<string, KafkaTopicProducer>();
        private readonly IList<KafkaGroupConsumerBase> _groupConsumers = new List<KafkaGroupConsumerBase>();

        public KafkaMessageBus(MessageBusSettings settings, KafkaMessageBusSettings kafkaSettings)
            : base(settings)
        {
            KafkaSettings = kafkaSettings;

            Log.Info("Creating producers");
            _producer = new Producer(kafkaSettings.BrokerList);
            foreach (var topicName in Settings.Publishers.Select(x => x.DefaultTopic).Distinct())
            {
                AddTopicProducerSafe(topicName);
            }

            Log.Info("Creating subscribers");
            foreach (var subscribersByGroup in settings.Subscribers.GroupBy(x => x.Group))
            {
                var group = subscribersByGroup.Key;

                foreach (var subscribersByMessageType in subscribersByGroup.GroupBy(x => x.MessageType))
                {
                    var messageType = subscribersByMessageType.Key;

                    Log.InfoFormat("Creating consumer for topics {0}, group {1}, message type {2}", string.Join(",", subscribersByMessageType.Select(x => x.Topic)), group, messageType);
                    var consumer = new KafkaGroupConsumer(this, group, messageType, subscribersByMessageType.ToList());
                    _groupConsumers.Add(consumer);
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
                groupConsumer.DisposeSilently(e => Log.WarnFormat("Error occured while disposing consumer group {0}. {1}", groupConsumer.Group, e));
            }
            _groupConsumers.Clear();

            foreach (var topic in _topicProducers.Values)
            {
                topic.DisposeSilently(e => Log.WarnFormat("Error occured while disposing topic {0}. {1}", topic.Name, e));
            }
            _topicProducers.Clear();

            if (_producer != null)
            {
                _producer.DisposeSilently(e => Log.WarnFormat("Error occured while disposing producer. {0}", e));
                _producer = null;
            }

            base.Dispose();
        }

        public override async Task Publish(Type messageType, byte[] payload, string topic)
        {
            // lookup the Kafka topic
            var kafkaTopic = GetTopicProducerSafe(topic);
            // send the message to topic
            var deliveryReport = await kafkaTopic.Topic.Produce(payload);
            // log some debug information
            Log.DebugFormat("Delivered message with offset {0} and partition {1}", deliveryReport.Offset, deliveryReport.Partition);
        }

        #endregion

        protected KafkaTopicProducer AddTopicProducerSafe(string topic)
        {
            KafkaTopicProducer kafkaTopic;
            // The lock is used to ensure, that during add, only one instance of kafkaTopic instance is created.
            // Without the lock, multiple instances of the kafka topic could have been created.
            lock (_topicProducers)
            {
                if (!_topicProducers.TryGetValue(topic, out kafkaTopic))
                {
                    Log.DebugFormat("Creating topic producer {0}", topic);
                    kafkaTopic = new KafkaTopicProducer(topic, _producer);
                    _topicProducers.TryAdd(topic, kafkaTopic);
                }
            }
            return kafkaTopic;
        }

        protected KafkaTopicProducer GetTopicProducerSafe(string topic)
        {
            // lookup the Kafka topic
            KafkaTopicProducer kafkaTopic;
            if (!_topicProducers.TryGetValue(topic, out kafkaTopic))
            {
                // when the Kafka topic producer does not exist create one
                kafkaTopic = AddTopicProducerSafe(topic);
            }
            return kafkaTopic;
        }
    }
}