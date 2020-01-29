using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Common.Logging;
using Confluent.Kafka;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.Kafka.Configs;

namespace SlimMessageBus.Host.Kafka
{
    /// <summary>
    /// <see cref="IMessageBus"/> implementation for Apache Kafka.
    /// Note that internal driver Producer/Consumer are all thread-safe (see https://github.com/edenhill/librdkafka/issues/215)
    /// </summary>
    public class KafkaMessageBus : MessageBusBase
    {
        private static readonly ILog Log = LogManager.GetLogger<KafkaMessageBus>();

        public KafkaMessageBusSettings ProviderSettings { get; }

        private Producer _producer;
        private readonly IList<KafkaGroupConsumer> _groupConsumers = new List<KafkaGroupConsumer>();
        private readonly IDictionary<Type, Func<object, string, byte[]>> _keyProviders = new Dictionary<Type, Func<object, string, byte[]>>();
        private readonly IDictionary<Type, Func<object, string, int>> _partitionProviders = new Dictionary<Type, Func<object, string, int>>();

        public KafkaMessageBus(MessageBusSettings settings, KafkaMessageBusSettings providerSettings)
            : base(settings)
        {
            ProviderSettings = providerSettings ?? throw new ArgumentNullException(nameof(providerSettings));

            OnBuildProvider();

            // TODO: Auto start should be a setting
            Start();
        }

        protected override void Build()
        {
            base.Build();

            CreateProducer();
            CreateGroupConsumers();
            CreateProviders();
        }

        public Task Flush()
        {
            AssertActive();
            return Task.Run(() => _producer.Flush(-1));
        }

        public Producer CreateProducerInternal()
        {
            Log.Trace("Creating producer settings");
            var config = ProviderSettings.ProducerConfigFactory();
            config[KafkaConfigKeys.Servers] = ProviderSettings.BrokerList;
            Log.DebugFormat(CultureInfo.InvariantCulture, "Producer settings: {0}", config);
            var producer = ProviderSettings.ProducerFactory(config);
            return producer;
        }

        private void CreateProviders()
        {
            foreach (var publisherSettings in Settings.Producers)
            {
                var keyProvider = publisherSettings.GetKeyProvider();
                if (keyProvider != null)
                {
                    _keyProviders.Add(publisherSettings.MessageType, keyProvider);
                }

                var partitionProvider = publisherSettings.GetPartitionProvider();
                if (partitionProvider != null)
                {
                    _partitionProviders.Add(publisherSettings.MessageType, partitionProvider);
                }
            }
        }

        private void CreateProducer()
        {
            Log.Info("Creating producer...");
            _producer = CreateProducerInternal();
            Log.InfoFormat(CultureInfo.InvariantCulture, "Created producer {0}", _producer.Name);
        }

        private void CreateGroupConsumers()
        {
            Log.Info("Creating group consumers...");

            var responseConsumerCreated = false;

            IKafkaTopicPartitionProcessor ResponseProcessorFactory(TopicPartition tp, IKafkaCommitController cc) => new KafkaResponseProcessor(Settings.RequestResponse, tp, cc, this);

            foreach (var consumersByGroup in Settings.Consumers.GroupBy(x => x.GetGroup()))
            {
                var group = consumersByGroup.Key;
                var consumerByTopic = consumersByGroup.ToDictionary(x => x.Topic);

                IKafkaTopicPartitionProcessor ConsumerProcessorFactory(TopicPartition tp, IKafkaCommitController cc) => new KafkaConsumerProcessor(consumerByTopic[tp.Topic], tp, cc, this);

                var topics = consumerByTopic.Keys.ToList();
                var processorFactory = (Func<TopicPartition, IKafkaCommitController, IKafkaTopicPartitionProcessor>) ConsumerProcessorFactory;

                // if responses are used and shared with the regular consumers group
                if (Settings.RequestResponse != null && group == Settings.RequestResponse.GetGroup())
                {
                    // Note: response topic cannot be used in consumer topics - this is enforced in AssertSettings method
                    topics.Add(Settings.RequestResponse.Topic);

                    processorFactory = (tp, cc) => tp.Topic == Settings.RequestResponse.Topic
                        ? ResponseProcessorFactory(tp, cc)
                        : ConsumerProcessorFactory(tp, cc);

                    responseConsumerCreated = true;
                }

                AddGroupConsumer(group, topics.ToArray(), processorFactory);
            }

            if (Settings.RequestResponse != null && !responseConsumerCreated)
            {
                AddGroupConsumer(Settings.RequestResponse.GetGroup(), new[] { Settings.RequestResponse.Topic }, ResponseProcessorFactory);
            }

            Log.InfoFormat(CultureInfo.InvariantCulture, "Created {0} group consumers", _groupConsumers.Count);
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

        protected override void AssertSettings()
        {
            base.AssertSettings();

            if (Settings.RequestResponse != null)
            {
                Assert.IsTrue(Settings.RequestResponse.GetGroup() != null,
                    () => new ConfigurationMessageBusException("Request-response: group was not provided"));

                Assert.IsFalse(Settings.Consumers.Any(x => x.GetGroup() == Settings.RequestResponse.GetGroup() && x.Topic == Settings.RequestResponse.Topic),
                    () => new ConfigurationMessageBusException("Request-response: cannot use topic that is already being used by a consumer"));
            }
        }

        #region Overrides of BaseMessageBus

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Flush().Wait();

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
            }
            base.Dispose(disposing);
        }

        public override async Task ProduceToTransport(Type messageType, object message, string name, byte[] payload)
        {
            AssertActive();

            // calculate message key
            var key = GetMessageKey(messageType, message, name);

            // calculate partition
            var partition = GetMessagePartition(messageType, message, name);

            Log.TraceFormat(CultureInfo.InvariantCulture, "Producing message {0} of type {1}, on topic {2}, partition {3}, key length {4}, payload size {5}", 
                message, messageType.Name, name, partition, key?.Length ?? 0, payload.Length);

            // send the message to topic
            var task = partition == NoPartition
                ? _producer.ProduceAsync(name, key, payload)
                : _producer.ProduceAsync(name, key, 0, key?.Length ?? 0, payload, 0, payload.Length, partition);
            
            var deliveryReport = await task.ConfigureAwait(false);
            if (deliveryReport.Error.HasError)
            {
                throw new PublishMessageBusException($"Error while publish message {message} of type {messageType.Name} to topic {name}. Kafka response code: {deliveryReport.Error.Code}, reason: {deliveryReport.Error.Reason}");
            }

            // log some debug information
            Log.DebugFormat(CultureInfo.InvariantCulture, "Message {0} of type {1} delivered to topic-partition-offset {2}", message, messageType.Name, deliveryReport.TopicPartitionOffset);
        }

        protected byte[] GetMessageKey(Type messageType, object message, string topic)
        {
            byte[] key = null;
            if (_keyProviders.TryGetValue(messageType, out var keyProvider))
            {
                key = keyProvider(message, topic);

                if (Log.IsDebugEnabled)
                {
                    Log.DebugFormat(CultureInfo.InvariantCulture, "The message {0} type {1} calculated key is {2} (Base64)", message, messageType.Name, Convert.ToBase64String(key));
                }
            }
            return key;
        }

        private const int NoPartition = -1;

        protected int GetMessagePartition(Type messageType, object message, string topic)
        {
            var partition = NoPartition;
            if (_partitionProviders.TryGetValue(messageType, out var partitionProvider))
            {
                partition = partitionProvider(message, topic);

                if (Log.IsDebugEnabled)
                {
                    Log.DebugFormat(CultureInfo.InvariantCulture, "The message {0} type {1} calculated partition is {2}", message, messageType.Name, partition);
                }
            }
            return partition;
        }

        #endregion
    }
}