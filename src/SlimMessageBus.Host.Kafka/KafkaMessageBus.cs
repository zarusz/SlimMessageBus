namespace SlimMessageBus.Host.Kafka
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Confluent.Kafka;
    using Microsoft.Extensions.Logging;
    using SlimMessageBus.Host.Config;
    using Message = Confluent.Kafka.Message<byte[], byte[]>;
    using IProducer = Confluent.Kafka.IProducer<byte[], byte[]>;
    using System.Diagnostics.CodeAnalysis;
    using SlimMessageBus.Host.Serialization;
    using System.Threading;

    /// <summary>
    /// <see cref="IMessageBus"/> implementation for Apache Kafka.
    /// Note that internal driver Producer/Consumer are all thread-safe (see https://github.com/edenhill/librdkafka/issues/215)
    /// </summary>
    public class KafkaMessageBus : MessageBusBase
    {
        private readonly ILogger logger;

        public KafkaMessageBusSettings ProviderSettings { get; }

        private IProducer producer;
        private readonly IList<KafkaGroupConsumer> groupConsumers = new List<KafkaGroupConsumer>();

        public KafkaMessageBus(MessageBusSettings settings, KafkaMessageBusSettings providerSettings)
            : base(settings)
        {
            logger = LoggerFactory.CreateLogger<KafkaMessageBus>();
            ProviderSettings = providerSettings ?? throw new ArgumentNullException(nameof(providerSettings));

            OnBuildProvider();
        }

        public IMessageSerializer HeaderSerializer
            => ProviderSettings.HeaderSerializer ?? Serializer;

        protected override void Build()
        {
            base.Build();

            CreateProducer();
            CreateGroupConsumers();
        }

        public void Flush()
        {
            AssertActive();
            producer.Flush();
        }

        public IProducer CreateProducerInternal()
        {
            logger.LogTrace("Creating producer settings");
            var config = new ProducerConfig()
            {
                BootstrapServers = ProviderSettings.BrokerList
            };
            ProviderSettings.ProducerConfig(config);

            logger.LogDebug("Producer settings: {ProducerSettings}", config);
            var producer = ProviderSettings.ProducerBuilderFactory(config).Build();
            return producer;
        }

        private void CreateProducer()
        {
            logger.LogInformation("Creating producer...");
            producer = CreateProducerInternal();
            logger.LogInformation("Created producer {Count}", producer.Name);
        }

        private void CreateGroupConsumers()
        {
            logger.LogInformation("Creating group consumers...");

            var responseConsumerCreated = false;

            IKafkaPartitionConsumer ResponseProcessorFactory(TopicPartition tp, IKafkaCommitController cc) => new KafkaPartitionConsumerForResponses(Settings.RequestResponse, tp, cc, this, HeaderSerializer);

            foreach (var consumersByGroup in Settings.Consumers.GroupBy(x => x.GetGroup()))
            {
                var group = consumersByGroup.Key;
                var consumerByTopic = consumersByGroup.ToDictionary(x => x.Path);

                IKafkaPartitionConsumer ConsumerProcessorFactory(TopicPartition tp, IKafkaCommitController cc) => new KafkaPartitionConsumerForConsumers(consumerByTopic[tp.Topic], tp, cc, this, HeaderSerializer);

                var topics = consumerByTopic.Keys.ToList();
                var processorFactory = (Func<TopicPartition, IKafkaCommitController, IKafkaPartitionConsumer>)ConsumerProcessorFactory;

                // if responses are used and shared with the regular consumers group
                if (Settings.RequestResponse != null && group == Settings.RequestResponse.GetGroup())
                {
                    // Note: response topic cannot be used in consumer topics - this is enforced in AssertSettings method
                    topics.Add(Settings.RequestResponse.Path);

                    processorFactory = (tp, cc) => tp.Topic == Settings.RequestResponse.Path
                        ? ResponseProcessorFactory(tp, cc)
                        : ConsumerProcessorFactory(tp, cc);

                    responseConsumerCreated = true;
                }

                AddGroupConsumer(group, topics.ToArray(), processorFactory);
            }

            if (Settings.RequestResponse != null && !responseConsumerCreated)
            {
                AddGroupConsumer(Settings.RequestResponse.GetGroup(), new[] { Settings.RequestResponse.Path }, ResponseProcessorFactory);
            }

            logger.LogInformation("Created {ConsumerGroupCount} group consumers", groupConsumers.Count);
        }

        protected async override Task OnStart()
        {
            await base.OnStart();

            logger.LogInformation("Group consumers starting...");
            foreach (var groupConsumer in groupConsumers)
            {
                groupConsumer.Start();
            }
            logger.LogInformation("Group consumers started");
        }

        private void AddGroupConsumer(string group, string[] topics, Func<TopicPartition, IKafkaCommitController, IKafkaPartitionConsumer> processorFactory)
        {
            groupConsumers.Add(new KafkaGroupConsumer(this, group, topics, processorFactory));
        }

        protected override void AssertSettings()
        {
            base.AssertSettings();

            foreach (var consumer in Settings.Consumers)
            {
                Assert.IsTrue(consumer.GetGroup() != null,
                    () => new ConfigurationMessageBusException($"Consumer ({consumer.MessageType}): group was not provided"));
            }

            if (Settings.RequestResponse != null)
            {
                Assert.IsTrue(Settings.RequestResponse.GetGroup() != null,
                    () => new ConfigurationMessageBusException("Request-response: group was not provided"));

                Assert.IsFalse(Settings.Consumers.Any(x => x.GetGroup() == Settings.RequestResponse.GetGroup() && x.Path == Settings.RequestResponse.Path),
                    () => new ConfigurationMessageBusException("Request-response: cannot use topic that is already being used by a consumer"));
            }
        }

        #region Overrides of BaseMessageBus

        protected override async ValueTask DisposeAsyncCore()
        {
            await base.DisposeAsyncCore();

            Flush();

            if (groupConsumers.Count > 0)
            {
                foreach (var groupConsumer in groupConsumers)
                {
                    groupConsumer.DisposeSilently(() => $"consumer group {groupConsumer.Group}", logger);
                }

                groupConsumers.Clear();
            }

            if (producer != null)
            {
                producer.DisposeSilently("producer", logger);
                producer = null;
            }
        }

        public override async Task ProduceToTransport(object message, string path, byte[] messagePayload, IDictionary<string, object> messageHeaders, CancellationToken cancellationToken)
        {
            AssertActive();

            var messageType = message.GetType();
            var producerSettings = GetProducerSettings(messageType);

            // calculate message key
            var key = GetMessageKey(producerSettings, messageType, message, path);

            var kafkaMessage = new Message { Key = key, Value = messagePayload };

            if (messageHeaders != null && messageHeaders.Count > 0)
            {
                kafkaMessage.Headers = new Headers();

                foreach (var keyValue in messageHeaders)
                {
                    var valueBytes = HeaderSerializer.Serialize(typeof(object), keyValue.Value);
                    kafkaMessage.Headers.Add(keyValue.Key, valueBytes);
                }
            }

            // calculate partition
            var partition = GetMessagePartition(producerSettings, messageType, message, path);

            logger.LogTrace("Producing message {Message} of type {MessageType}, on topic {Topic}, partition {Partition}, key size {KeySize}, payload size {MessageSize}",
                message, messageType.Name, path, partition, key?.Length ?? 0, messagePayload.Length);

            // send the message to topic
            var task = partition == NoPartition
                ? producer.ProduceAsync(path, kafkaMessage, cancellationToken: cancellationToken)
                : producer.ProduceAsync(new TopicPartition(path, new Partition(partition)), kafkaMessage, cancellationToken: cancellationToken);

            // ToDo: Introduce support for not awaited produce

            var deliveryResult = await task.ConfigureAwait(false);
            if (deliveryResult.Status == PersistenceStatus.NotPersisted)
            {
                throw new PublishMessageBusException($"Error while publish message {message} of type {messageType.Name} to topic {path}. Kafka persistence status: {deliveryResult.Status}");
            }

            // log some debug information
            logger.LogDebug("Message {Message} of type {MessageType} delivered to topic {Topic}, partition {Partition}, offset: {Offset}",
                message, messageType.Name, deliveryResult.Topic, deliveryResult.Partition, deliveryResult.Offset);
        }

        protected byte[] GetMessageKey(ProducerSettings producerSettings, [NotNull] Type messageType, object message, string topic)
        {
            var keyProvider = producerSettings.GetKeyProvider();
            if (keyProvider != null)
            {
                var key = keyProvider(message, topic);

                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug("The message {Message} type {MessageType} calculated key is {Key} (Base64)", message, messageType.Name, Convert.ToBase64String(key));
                }

                return key;
            }
            return null;
        }

        private const int NoPartition = -1;

        protected int GetMessagePartition(ProducerSettings producerSettings, [NotNull] Type messageType, object message, string topic)
        {
            var partitionProvider = producerSettings.GetPartitionProvider();
            if (partitionProvider != null)
            {
                var partition = partitionProvider(message, topic);

                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug("The Message {Message} type {MessageType} calculated partition is {Partition}", message, messageType.Name, partition);
                }

                return partition;
            }
            return NoPartition;
        }

        #endregion
    }
}