using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Logging;
using Confluent.Kafka;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.Kafka
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
        private readonly IList<KafkaGroupConsumerBase> _groupConsumers = new List<KafkaGroupConsumerBase>();

        public Producer CreateProducer()
        {
            Log.Debug("Creating producer settings");
            var config = KafkaSettings.ProducerConfigFactory();
            config[KafkaConfigKeys.Servers] = KafkaSettings.BrokerList;
            Log.DebugFormat("Producer settings: {0}", config);
            var producer = KafkaSettings.ProducerFactory(config);
            return producer;
        }

        public KafkaMessageBus(MessageBusSettings settings, KafkaMessageBusSettings kafkaSettings)
            : base(settings)
        {
            AssertSettings(settings);

            KafkaSettings = kafkaSettings;

            Log.Info("Creating producer");
            _producer = CreateProducer();
            Log.InfoFormat("Producer has been assigned name: {0}", _producer.Name);

            Log.Info("Creating subscribers");
            foreach (var subscribersByGroup in settings.Consumers.GroupBy(x => x.Group))
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

        private static void AssertSettings(MessageBusSettings settings)
        {
            if (settings.RequestResponse != null)
            {
                Assert.IsTrue(settings.RequestResponse.Group != null,
                    () => new InvalidConfigurationMessageBusException($"Request-response: group was not provided"));
            }
        }

        #region Overrides of BaseMessageBus

        protected override void OnDispose()
        {
            foreach (var groupConsumer in _groupConsumers)
            {
                groupConsumer.DisposeSilently(() => $"consumer group {groupConsumer.Group}", Log);
            }
            _groupConsumers.Clear();

            if (_producer != null)
            {
                _producer.DisposeSilently("producer", Log);
                _producer = null;
            }

            base.OnDispose();
        }

        public override async Task Publish(Type messageType, byte[] payload, string topic)
        {
            Log.DebugFormat("Producing message of type {0} on topic {1} with size {2}", messageType.Name, topic, payload.Length);
            // send the message to topic
            var deliveryReport = await _producer.ProduceAsync(topic, null, payload);
            // log some debug information
            Log.DebugFormat("Delivered message at {0}", deliveryReport.TopicPartitionOffset);
        }

        #endregion
    }
}