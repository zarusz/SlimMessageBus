namespace SlimMessageBus.Host.AmazonSQS;

using SlimMessageBus.Host.Services;

public class SqsMessageBus : MessageBusBase<SqsMessageBusSettings>
{
    private readonly ILogger _logger;

    private readonly ISqsClientProvider _clientProviderSqs;
    private readonly ISnsClientProvider _clientProviderSns;

    internal SqsTopologyCache TopologyCache { get; }

    public ISqsHeaderSerializer<Amazon.SQS.Model.MessageAttributeValue> SqsHeaderSerializer { get; }
    public ISqsHeaderSerializer<Amazon.SimpleNotificationService.Model.MessageAttributeValue> SnsHeaderSerializer { get; }
    protected override IMessageBusSettingsValidationService ValidationService => new SqsMessageBusSettingsValidationService(Settings);

    public SqsMessageBus(MessageBusSettings settings, SqsMessageBusSettings providerSettings)
        : base(settings, providerSettings)
    {
        _logger = LoggerFactory.CreateLogger<SqsMessageBus>();

        _clientProviderSqs = settings.ServiceProvider.GetRequiredService<ISqsClientProvider>();
        _clientProviderSns = settings.ServiceProvider.GetRequiredService<ISnsClientProvider>();

        SqsHeaderSerializer = providerSettings.SqsHeaderSerializer;
        SnsHeaderSerializer = providerSettings.SnsHeaderSerializer;

        TopologyCache = new SqsTopologyCache(_clientProviderSqs, _clientProviderSns);

        OnBuildProvider();
    }

    protected override void Build()
    {
        base.Build();
        InitTaskList.Add(InitAsync, CancellationToken);
    }

    private IMessageSerializer<string> GetMessageSerializer(string path) => SerializerProvider.GetSerializer(path) as IMessageSerializer<string>
        ?? throw new ConfigurationMessageBusException($"Serializer for Amazon SQS must be able to serialize into a string (it needs to implement {nameof(IMessageSerializer<string>)})");

    protected override async Task CreateConsumers()
    {
        await base.CreateConsumers();

        void AddQueueConsumer(string queue, IMessageProcessor<SqsTransportMessageWithPayload> messageProcessor, IEnumerable<AbstractConsumerSettings> consumerSettings, IMessageSerializer<string> messageSerializer)
        {
            _logger.LogInformation("Creating consumer for Queue: {Queue}", queue);
            var consumer = new SqsQueueConsumer(this, queue, _clientProviderSqs, messageProcessor, messageSerializer, consumerSettings);
            AddConsumer(consumer);
        }

        static void InitConsumerContext(SqsTransportMessageWithPayload t, ConsumerContext ctx) => ctx.SetTransportMessage(t.TransportMessage);

        foreach (var (queue, consumerSettings) in Settings.Consumers
                .GroupBy(x => x.GetOrDefault(SqsProperties.UnderlyingQueue))
                .Where(x => x.Key != null) // The SqsMessageBusSettingsValidationService will ensure that the queue is set, but just in case
                .ToDictionary(x => x.Key, x => x.ToList()))
        {
            var messageSerializer = GetMessageSerializer(queue);
            object MessageProvider(Type messageType, SqsTransportMessageWithPayload transportMessage) => messageSerializer.Deserialize(messageType, transportMessage.Payload);

            var messageProcessor = new MessageProcessor<SqsTransportMessageWithPayload>(
                consumerSettings,
                this,
                messageProvider: MessageProvider,
                path: queue,
                responseProducer: this,
                consumerContextInitializer: InitConsumerContext,
                consumerErrorHandlerOpenGenericType: typeof(ISqsConsumerErrorHandler<>));

            AddQueueConsumer(queue, messageProcessor, consumerSettings, messageSerializer);
        }

        if (Settings.RequestResponse != null)
        {
            var queue = Settings.RequestResponse.GetOrDefault(SqsProperties.UnderlyingQueue);

            var messageSerializer = GetMessageSerializer(queue);
            object MessageProvider(Type messageType, SqsTransportMessageWithPayload transportMessage) => messageSerializer.Deserialize(messageType, transportMessage.Payload);

            var messageProcessor = new ResponseMessageProcessor<SqsTransportMessageWithPayload>(
                LoggerFactory,
                Settings.RequestResponse,
                messageProvider: MessageProvider,
                PendingRequestStore,
                TimeProvider);

            AddQueueConsumer(queue, messageProcessor, [Settings.RequestResponse], messageSerializer);
        }
    }

    /// <summary>
    /// Performs initialization that has to happen before the first message produce happens.
    /// </summary>
    /// <returns></returns>
    private async Task InitAsync()
    {
        try
        {
            _logger.LogInformation("Ensuring client is authenticate");
            // Ensure the client finished the first authentication
            await _clientProviderSqs.EnsureClientAuthenticated();
            await _clientProviderSns.EnsureClientAuthenticated();

            // Provision the topology if enabled
            if (ProviderSettings.TopologyProvisioning?.Enabled ?? false)
            {
                _logger.LogInformation("Provisioning topology");
                await ProvisionTopology();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SQS Transport initialization failed: {ErrorMessage}", ex.Message);
        }
    }

    public override async Task ProvisionTopology()
    {
        await base.ProvisionTopology();

        var provisioningService = new SqsTopologyService(LoggerFactory.CreateLogger<SqsTopologyService>(),
                                                         Settings,
                                                         ProviderSettings,
                                                         _clientProviderSqs,
                                                         _clientProviderSns,
                                                         TopologyCache);

        await provisioningService.ProvisionTopology(CancellationToken); // provisioning happens asynchronously
    }

    private async Task<(IMessageSerializer<string> messageSerializer, SqsPathMeta pathMeta)> GetMetaForPath(string path, CancellationToken cancellationToken)
    {
        var messageSerializer = GetMessageSerializer(path);

        // Note: When a path not declared during bus producer/consumer declarations (it is dynamic), e.g. for RequestResponse - the path kind is not known at this point, so we assume it is a queue
        // See SqsRequestResponseBuilderExtensions.ReplyToQueue
        var pathMeta = await TopologyCache.GetMetaWithPreloadOrException(path, PathKind.Queue, cancellationToken);

        return (messageSerializer, pathMeta);
    }

    public override async Task ProduceToTransport(object message, Type messageType, string path, IDictionary<string, object> messageHeaders, IMessageBusTarget targetBus, CancellationToken cancellationToken)
    {
        OnProduceToTransport(message, messageType, path, messageHeaders);

        var (messageSerializer, pathMeta) = await GetMetaForPath(path, cancellationToken);

        try
        {

            if (pathMeta.PathKind == PathKind.Queue)
            {
                var (payload, attributes, deduplicationId, groupId) = GetTransportMessage(message, messageType, messageHeaders, messageSerializer, SqsHeaderSerializer);
                await _clientProviderSqs.Client.SendMessageAsync(new SendMessageRequest(pathMeta.Url, payload)
                {
                    MessageAttributes = attributes,
                    MessageDeduplicationId = deduplicationId,
                    MessageGroupId = groupId
                }, cancellationToken);
            }
            else
            {
                var (payload, attributes, deduplicationId, groupId) = GetTransportMessage(message, messageType, messageHeaders, messageSerializer, SnsHeaderSerializer);
                await _clientProviderSns.Client.PublishAsync(new PublishRequest(pathMeta.Arn, payload)
                {
                    MessageAttributes = attributes,
                    MessageDeduplicationId = deduplicationId,
                    MessageGroupId = groupId,
                }, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not ProducerMessageBusException && ex is not TaskCanceledException)
        {
            throw new ProducerMessageBusException(GetProducerErrorMessage(path, message, messageType, ex), ex);
        }
    }

    // Chunk if exceeds 10 messages and payload size (Amazon SQS limits)
    private const int MaxMessagesInBatch = 10;

    public override async Task<ProduceToTransportBulkResult<T>> ProduceToTransportBulk<T>(IReadOnlyCollection<T> envelopes, string path, IMessageBusTarget targetBus, CancellationToken cancellationToken)
    {
        var dispatched = new List<T>(envelopes.Count);
        try
        {
            var (messageSerializer, pathMeta) = await GetMetaForPath(path, cancellationToken);

            if (pathMeta.PathKind == PathKind.Queue)
            {
                var entries = new List<SendMessageBatchRequestEntry>(MaxMessagesInBatch);

                var envelopeChunks = envelopes.Chunk(MaxMessagesInBatch);
                foreach (var envelopeChunk in envelopeChunks)
                {
                    foreach (var envelope in envelopeChunk)
                    {
                        var (payload, attributes, deduplicationId, groupId) = GetTransportMessage(envelope.Message, envelope.MessageType, envelope.Headers, messageSerializer, SqsHeaderSerializer);

                        entries.Add(new SendMessageBatchRequestEntry(Guid.NewGuid().ToString(), payload)
                        {
                            MessageAttributes = attributes,
                            MessageDeduplicationId = deduplicationId,
                            MessageGroupId = groupId
                        });
                    }

                    await _clientProviderSqs.Client.SendMessageBatchAsync(new SendMessageBatchRequest(pathMeta.Url, entries), cancellationToken);

                    entries.Clear();

                    dispatched.AddRange(envelopeChunk);
                }
            }
            else
            {
                var entries = new List<PublishBatchRequestEntry>(MaxMessagesInBatch);

                var envelopeChunks = envelopes.Chunk(MaxMessagesInBatch);
                foreach (var envelopeChunk in envelopeChunks)
                {
                    foreach (var envelope in envelopeChunk)
                    {
                        var (payload, attributes, deduplicationId, groupId) = GetTransportMessage(envelope.Message, envelope.MessageType, envelope.Headers, messageSerializer, SnsHeaderSerializer);

                        entries.Add(new PublishBatchRequestEntry
                        {
                            Id = Guid.NewGuid().ToString(),
                            Message = payload,
                            MessageAttributes = attributes,
                            MessageDeduplicationId = deduplicationId,
                            MessageGroupId = groupId
                        });
                    }

                    await _clientProviderSns.Client.PublishBatchAsync(new PublishBatchRequest { TopicArn = pathMeta.Arn, PublishBatchRequestEntries = entries }, cancellationToken);

                    entries.Clear();

                    dispatched.AddRange(envelopeChunk);
                }
            }

            return new(dispatched, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Producing message batch to path {Path} resulted in error {Error}", path, ex.Message);
            return new(dispatched, ex);
        }
    }

    private (string Payload, Dictionary<string, THeaderValue> Attributes, string DeduplicationId, string GroupId) GetTransportMessage<THeaderValue>(
        object message,
        Type messageType,
        IDictionary<string, object> messageHeaders,
        IMessageSerializer<string> messageSerializer,
        ISqsHeaderSerializer<THeaderValue> headerSerializer)
        where THeaderValue : class
    {
        var producerSettings = GetProducerSettings(messageType);

        var messageDeduplicationIdProvider = producerSettings.GetOrDefault(SqsProperties.MessageDeduplicationId, null);
        var deduplicationId = messageDeduplicationIdProvider?.Invoke(message, messageHeaders);

        var messageGroupIdProvider = producerSettings.GetOrDefault(SqsProperties.MessageGroupId, null);
        var groupId = messageGroupIdProvider?.Invoke(message, messageHeaders);

        var messageAttributes = GetTransportMessageAttibutes(messageHeaders, headerSerializer);

        var messagePayload = messageSerializer.Serialize(messageType, message);

        return (messagePayload, messageAttributes, deduplicationId, groupId);
    }

    private static Dictionary<string, THeaderValue> GetTransportMessageAttibutes<THeaderValue>(IDictionary<string, object> messageHeaders, ISqsHeaderSerializer<THeaderValue> headerSerializer)
        where THeaderValue : class
    {
        if (messageHeaders is null)
        {
            return null;
        }

        var messageAttributes = new Dictionary<string, THeaderValue>(messageHeaders.Count);

        foreach (var header in messageHeaders)
        {
            var headerValue = headerSerializer.Serialize(header.Key, header.Value);
            messageAttributes.Add(header.Key, headerValue);
        }

        return messageAttributes;
    }
}
