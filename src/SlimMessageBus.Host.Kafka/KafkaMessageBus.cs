namespace SlimMessageBus.Host.Kafka;

using IProducer = Confluent.Kafka.IProducer<byte[], byte[]>;
using Message = Confluent.Kafka.Message<byte[], byte[]>;

/// <summary>
/// <see cref="IMessageBus"/> implementation for Apache Kafka.
/// Note that internal driver Producer/Consumer are all thread-safe (see https://github.com/edenhill/librdkafka/issues/215)
/// </summary>
public class KafkaMessageBus : MessageBusBase<KafkaMessageBusSettings>
{
    private readonly ILogger _logger;
    private IProducer _producer;
    private bool _producerDeliveryReportsEnabled;

    public KafkaMessageBus(MessageBusSettings settings, KafkaMessageBusSettings providerSettings)
        : base(settings, providerSettings)
    {
        _logger = LoggerFactory.CreateLogger<KafkaMessageBus>();

        OnBuildProvider();
    }

    public IMessageSerializer HeaderSerializer
        => ProviderSettings.HeaderSerializer ?? Serializer;

    protected override IMessageBusSettingsValidationService ValidationService => new KafkaMessageBusSettingsValidationService(Settings, ProviderSettings);

    protected override void Build()
    {
        base.Build();

        _logger.LogInformation("Creating producers for {BusName} bus...", Name);
        _producer = CreateProducerInternal();
    }

    public void Flush()
    {
        AssertActive();
        _producer.Flush();
    }

    public IProducer CreateProducerInternal()
    {
        _logger.LogTrace("Creating producer settings");
        var config = new ProducerConfig
        {
            BootstrapServers = ProviderSettings.BrokerList,
        };
        ProviderSettings.ProducerConfig(config);

        _producerDeliveryReportsEnabled = config.EnableDeliveryReports ?? true; // when not set per Confluent.KafkaNet docs, it's defaulting to true

        _logger.LogDebug("Producer settings: {ProducerSettings}", config);
        return ProviderSettings.ProducerBuilderFactory(config).Build();
    }

    protected override async Task CreateConsumers()
    {
        await base.CreateConsumers();

        var responseConsumerCreated = false;

        void AddGroupConsumer(string group, IReadOnlyCollection<string> topics, Func<TopicPartition, IKafkaCommitController, IKafkaPartitionConsumer> processorFactory)
        {
            _logger.LogInformation("Creating consumer group {ConsumerGroup}", group);
            AddConsumer(new KafkaGroupConsumer(LoggerFactory, ProviderSettings, group, topics, processorFactory));
        }

        IKafkaPartitionConsumer ResponseProcessorFactory(TopicPartition tp, IKafkaCommitController cc) => new KafkaPartitionConsumerForResponses(LoggerFactory, Settings.RequestResponse, Settings.RequestResponse.GetGroup(), tp, cc, this, HeaderSerializer);

        foreach (var consumersByGroup in Settings.Consumers.GroupBy(x => x.GetGroup()))
        {
            var group = consumersByGroup.Key;
            var consumersByTopic = consumersByGroup.GroupBy(x => x.Path).ToDictionary(x => x.Key, x => x.ToArray());
            var topics = consumersByTopic.Keys.ToList();

            IKafkaPartitionConsumer ConsumerProcessorFactory(TopicPartition tp, IKafkaCommitController cc) => new KafkaPartitionConsumerForConsumers(LoggerFactory, consumersByTopic[tp.Topic], group, tp, cc, HeaderSerializer, this);

            var processorFactory = ConsumerProcessorFactory;

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

            AddGroupConsumer(group, topics, processorFactory);
        }

        if (Settings.RequestResponse != null && !responseConsumerCreated)
        {
            AddGroupConsumer(Settings.RequestResponse.GetGroup(), [Settings.RequestResponse.Path], ResponseProcessorFactory);
        }
    }

    #region Overrides of BaseMessageBus

    protected override async ValueTask DisposeAsyncCore()
    {
        Flush();

        await base.DisposeAsyncCore();

        if (_producer != null)
        {
            _producer.DisposeSilently("producer", _logger);
            _producer = null;
        }
    }

    protected override async Task<ProduceToTransportBulkResult<T>> ProduceToTransportBulk<T>(IReadOnlyCollection<T> envelopes, string path, IMessageBusTarget targetBus, CancellationToken cancellationToken)
    {
        AssertActive();

        static void OnMessageDelivered(ILogger logger, DeliveryResult<byte[], byte[]> deliveryResult, object message, Type messageType)
        {
            // log some debug information
            logger.LogDebug("Message {Message} of type {MessageType} delivered to topic {Topic}, partition {Partition}, offset: {Offset}",
                message, messageType?.Name, deliveryResult.Topic, deliveryResult.Partition, deliveryResult.Offset);
        }

        var dispatched = new List<T>(envelopes.Count);
        try
        {
            foreach (var envelope in envelopes)
            {
                var messageType = envelope.Message?.GetType();
                var producerSettings = messageType != null ? GetProducerSettings(messageType) : null;
                var messagePayload = Serializer.Serialize(envelope.MessageType, envelope.Message);

                // calculate message key
                var key = GetMessageKey(producerSettings, messageType, envelope.Message, path);
                var transportMessage = new Message { Key = key, Value = messagePayload };

                if (envelope.Headers != null && envelope.Headers.Count > 0)
                {
                    transportMessage.Headers = [];

                    foreach (var keyValue in envelope.Headers)
                    {
                        var valueBytes = HeaderSerializer.Serialize(typeof(object), keyValue.Value);
                        transportMessage.Headers.Add(keyValue.Key, valueBytes);
                    }
                }

                // calculate partition
                var partition = producerSettings != null
                    ? GetMessagePartition(producerSettings, messageType, envelope.Message, path)
                    : NoPartition;

                _logger.LogDebug("Producing message {Message} of type {MessageType}, topic {Topic}, partition {Partition}, key size {KeySize}, payload size {MessageSize}, headers count {MessageHeaderCount}",
                    envelope.Message,
                    messageType?.Name,
                    path,
                    partition,
                    key?.Length ?? 0,
                    messagePayload?.Length ?? 0,
                    transportMessage.Headers?.Count ?? 0);

                var topicPartition = partition == NoPartition ? null : new TopicPartition(path, partition);

                // check if we should await for the message delivery result, by default true
                var awaitProduceResult = producerSettings?.GetOrDefault(KafkaProducerSettingsExtensions.EnableProduceAwaitKey, Settings, true) ?? true;
                if (awaitProduceResult)
                {
                    // send the message to topic and await result
                    var task = topicPartition == null
                        ? _producer.ProduceAsync(path, transportMessage, cancellationToken: cancellationToken)
                        : _producer.ProduceAsync(topicPartition, transportMessage, cancellationToken: cancellationToken);

                    var deliveryResult = await task.ConfigureAwait(false);
                    if (deliveryResult.Status == PersistenceStatus.NotPersisted)
                    {
                        throw new ProducerMessageBusException($"Error while publish message {envelope.Message} of type {messageType?.Name} to topic {path}. Kafka persistence status: {deliveryResult.Status}");
                    }

                    // log some debug information
                    OnMessageDelivered(_logger, deliveryResult, envelope.Message, messageType);
                }
                else
                {
                    // callback for message delivery result, null if not enabled to save on delegate memory allocation
                    Action<DeliveryReport<byte[], byte[]>> onMessageFailed = _producerDeliveryReportsEnabled
                        ? (deliveryReport) =>
                        {
                            if (deliveryReport.Error.IsError)
                            {
                                _logger.LogError("Message {Message} of type {MessageType} failed to deliver to topic {Topic}, partition {Partition}, error: {Error}",
                                    envelope.Message, messageType?.Name, deliveryReport.Topic, deliveryReport.Partition, deliveryReport.Error);
                            }
                            else
                            {
                                OnMessageDelivered(_logger, deliveryReport, envelope.Message, messageType);
                            }
                        }
                    : null;

                    // send the message to topic and dont await result (potential message loss)
                    if (topicPartition == null)
                    {
                        _producer.Produce(path, transportMessage, onMessageFailed);
                    }
                    else
                    {
                        _producer.Produce(topicPartition, transportMessage, onMessageFailed);
                    }

                    // log some debug information
                    _logger.LogDebug("Message {Message} of type {MessageType} sent to topic {Topic} (result is not awaited)",
                        envelope.Message, messageType?.Name, path);
                }
                dispatched.Add(envelope);
            }
        }
        catch (Exception ex)
        {
            return new(dispatched, ex);
        }
        return new(dispatched, null);
    }

    protected byte[] GetMessageKey(ProducerSettings producerSettings, Type messageType, object message, string topic)
    {
        var keyProvider = producerSettings?.GetKeyProvider();
        if (keyProvider != null)
        {
            var key = keyProvider(message, topic);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("The message {Message} type {MessageType} calculated key is {Key} (Base64)", message, messageType?.Name, Convert.ToBase64String(key));
            }

            return key;
        }
        return [];
    }

    private const int NoPartition = -1;

    protected int GetMessagePartition(ProducerSettings producerSettings, Type messageType, object message, string topic)
    {
        var partitionProvider = producerSettings.GetPartitionProvider();
        if (partitionProvider != null)
        {
            var partition = partitionProvider(message, topic);

            _logger.LogDebug("The Message {Message} type {MessageType} calculated partition is {Partition}", message, messageType?.Name, partition);

            return partition;
        }
        return NoPartition;
    }

    #endregion
}
