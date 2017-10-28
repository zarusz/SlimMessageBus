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
    /// <see cref="IMessageBus"/> implementation for Apache Kafka.
    /// Note that internal driver Producer/Consumer are all thread-safe (see https://github.com/edenhill/librdkafka/issues/215)
    /// </summary>
    public class KafkaMessageBus : MessageBusBase
    {
        private static readonly ILog Log = LogManager.GetLogger<KafkaMessageBus>();

        public KafkaMessageBusSettings KafkaSettings { get; }

        private Producer _producer;
        private readonly IList<KafkaGroupConsumer> _groupConsumers = new List<KafkaGroupConsumer>();

        public Producer CreateProducerInternal()
        {
            Log.Trace("Creating producer settings");
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

            CreateProducer();
            CreateGroupConsumers(settings);

            Start();
        }

        private void CreateProducer()
        {
            Log.Info("Creating producer...");
            _producer = CreateProducerInternal();
            Log.InfoFormat("Created producer {0}", _producer.Name);
        }

        private void CreateGroupConsumers(MessageBusSettings settings)
        {
            Log.Info("Creating group consumers...");

            var responseConsumerCreated = false;

            Func<TopicPartition, IKafkaCommitController, IKafkaTopicPartitionProcessor> responseProcessorFactory =
                (tp, cc) => new KafkaResponseProcessor(settings.RequestResponse, tp, cc, this);

            foreach (var consumersByGroup in settings.Consumers.GroupBy(x => x.Group))
            {
                var group = consumersByGroup.Key;
                var consumerByTopic = consumersByGroup.ToDictionary(x => x.Topic);

                Func<TopicPartition, IKafkaCommitController, IKafkaTopicPartitionProcessor> consumerProcessorFactory = 
                    (tp, cc) => new KafkaConsumerProcessor(consumerByTopic[tp.Topic], tp, cc, this);

                var topics = consumerByTopic.Keys.ToList();
                var processorFactory = consumerProcessorFactory;

                // if responses are used and shared with the regular consumers group
                if (settings.RequestResponse != null && group == settings.RequestResponse.Group)
                {
                    // Note: response topic cannot be used in consumer topics - this is enforced in AssertSettings method
                    topics.Add(settings.RequestResponse.Topic);

                    processorFactory = (tp, cc) => tp.Topic == settings.RequestResponse.Topic
                        ? responseProcessorFactory(tp, cc)
                        : consumerProcessorFactory(tp, cc);

                    responseConsumerCreated = true;
                }

                AddGroupConsumer(group, topics.ToArray(), processorFactory);
            }

            if (settings.RequestResponse != null && !responseConsumerCreated)
            {
                AddGroupConsumer(settings.RequestResponse.Group, new[] { settings.RequestResponse.Topic }, responseProcessorFactory);
            }

            Log.InfoFormat("Created {0} group consumers", _groupConsumers.Count);
        }

        private void Start()
        {
            Log.Info("Starting group consumers...");
            foreach (var groupConsumer in _groupConsumers)
            {
                groupConsumer.Start();
            }
            Log.Info("Group consumers started");
        }

        private void AddGroupConsumer(string group, string[] topics, Func<TopicPartition, IKafkaCommitController, IKafkaTopicPartitionProcessor> processorFactory)
        {
            _groupConsumers.Add(new KafkaGroupConsumer(this, group, topics, processorFactory));
        }

        private static void AssertSettings(MessageBusSettings settings)
        {
            if (settings.RequestResponse != null)
            {
                Assert.IsTrue(settings.RequestResponse.Group != null,
                    () => new InvalidConfigurationMessageBusException($"Request-response: group was not provided"));

                Assert.IsFalse(settings.Consumers.Any(x => x.Group == settings.RequestResponse.Group && x.Topic == settings.RequestResponse.Topic),
                    () => new InvalidConfigurationMessageBusException($"Request-response: cannot use topic that is already being used by a consumer"));
            }
        }

        #region Overrides of BaseMessageBus

        protected override void OnDispose()
        {
            if (_groupConsumers.Count > 0)
            {
                foreach (var groupConsumer in _groupConsumers)
                {
                    groupConsumer.DisposeSilently(() => $"consumer group {groupConsumer.Group}", Log);
                }
                _groupConsumers.Clear();
            }

            if (_producer != null)
            {
                _producer.DisposeSilently("producer", Log);
                _producer = null;
            }

            base.OnDispose();
        }

        public override async Task Publish(Type messageType, byte[] payload, string topic)
        {
            AssertActive();

            Log.TraceFormat("Producing message of type {0} on topic {1} with size {2}", messageType.Name, topic, payload.Length);
            // send the message to topic
            var deliveryReport = await _producer.ProduceAsync(topic, null, payload);
            if (deliveryReport.Error.HasError)
            {
                throw new PublishMessageBusException($"Could not publish message of type ${messageType.Name} to topic ${topic}. Kafka response code: ${deliveryReport.Error.Code}, reason: ${deliveryReport.Error.Reason}");
            }
                
            // log some debug information
            Log.DebugFormat("Message of type {0} delivered at offset {1}", messageType.Name, deliveryReport.TopicPartitionOffset);
        }

        #endregion
    }
}