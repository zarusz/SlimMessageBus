namespace SlimMessageBus.Host.AmazonSQS;

using SlimMessageBus.Host.Serialization;

public class SqsMessageBus : MessageBusBase<SqsMessageBusSettings>
{
    private readonly ILogger _logger;
    private readonly ISqsClientProvider _clientProvider;
    private readonly Dictionary<string, string> _queueUrlByPath = [];

    public ISqsHeaderSerializer HeaderSerializer { get; }

    public SqsMessageBus(MessageBusSettings settings, SqsMessageBusSettings providerSettings)
        : base(settings, providerSettings)
    {
        _logger = LoggerFactory.CreateLogger<SqsMessageBus>();
        _clientProvider = settings.ServiceProvider.GetRequiredService<ISqsClientProvider>();
        HeaderSerializer = providerSettings.SqsHeaderSerializer;
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

        void AddConsumerFrom(string path, PathKind pathKind, IMessageProcessor<Message> messageProcessor, IEnumerable<AbstractConsumerSettings> consumerSettings)
        {
            if (pathKind == PathKind.Queue)
            {
                _logger.LogInformation("Creating consumer for Queue: {Queue}", path);
                var consumer = new SqsQueueConsumer(this, path, _clientProvider, messageProcessor, consumerSettings);
                AddConsumer(consumer);
            }
        }

        static void InitConsumerContext(Message m, ConsumerContext ctx) => ctx.SetTransportMessage(m);

        foreach (var ((path, pathKind), consumerSettings) in Settings.Consumers
                .GroupBy(x => (x.Path, x.PathKind))
                .ToDictionary(x => x.Key, x => x.ToList()))
        {
            var messageSerializer = GetMessageSerializer(path);
            object MessageProvider(Type messageType, Message transportMessage) => messageSerializer.Deserialize(messageType, transportMessage.Body);

            var messageProcessor = new MessageProcessor<Message>(
                consumerSettings,
                this,
                messageProvider: MessageProvider,
                path: path,
                responseProducer: this,
                consumerContextInitializer: InitConsumerContext,
                consumerErrorHandlerOpenGenericType: typeof(ISqsConsumerErrorHandler<>));

            AddConsumerFrom(path, pathKind, messageProcessor, consumerSettings);
        }

        if (Settings.RequestResponse != null)
        {
            var path = Settings.RequestResponse.Path;

            var messageSerializer = GetMessageSerializer(path);
            object MessageProvider(Type messageType, Message transportMessage) => messageSerializer.Deserialize(messageType, transportMessage.Body);

            var messageProcessor = new ResponseMessageProcessor<Message>(
                LoggerFactory,
                Settings.RequestResponse,
                messageProvider: MessageProvider,
                PendingRequestStore,
                TimeProvider);

            AddConsumerFrom(
                path,
                Settings.RequestResponse.PathKind,
                messageProcessor,
                [Settings.RequestResponse]);
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
            await _clientProvider.EnsureClientAuthenticated();

            // Provision the topology if enabled
            if (ProviderSettings.TopologyProvisioning?.Enabled ?? false)
            {
                _logger.LogInformation("Provisioning topology");
                await ProvisionTopology();
            }

            // Read the Queue/Topic URLs for the producers
            _logger.LogInformation("Populating queue URLs");
            await PopulatePathToUrlMappings();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SQS Transport initialization failed: {ErrorMessage}", ex.Message);
        }
    }

    public override async Task ProvisionTopology()
    {
        await base.ProvisionTopology();

        var provisioningService = new SqsTopologyService(LoggerFactory.CreateLogger<SqsTopologyService>(), Settings, ProviderSettings, _clientProvider);
        await provisioningService.ProvisionTopology(); // provisioning happens asynchronously
    }

    private async Task PopulatePathToUrlMappings()
    {
        var queuePaths = Settings.Producers.Where(x => x.PathKind == PathKind.Queue).Select(x => x.DefaultPath)
            .Concat(Settings.Consumers.Where(x => x.PathKind == PathKind.Queue).Select(x => x.Path))
            .Concat(Settings.RequestResponse?.PathKind == PathKind.Queue ? [Settings.RequestResponse.Path] : [])
            .ToHashSet();

        foreach (var queuePath in queuePaths)
        {
            try
            {
                _logger.LogDebug("Populating URL for queue {QueueName}", queuePath);
                var queueResponse = await _clientProvider.Client.GetQueueUrlAsync(queuePath, CancellationToken);
                _queueUrlByPath[queuePath] = queueResponse.QueueUrl;
            }
            catch (QueueDoesNotExistException ex)
            {
                _logger.LogError(ex, "Queue {QueueName} does not exist, ensure that it either exists or topology provisioning is enabled", queuePath);
            }
        }
    }
    internal string GetQueueUrlOrException(string path)
    {
        if (_queueUrlByPath.TryGetValue(path, out var queueUrl))
        {
            return queueUrl;
        }
        throw new ProducerMessageBusException($"Queue {path} has unknown URL at this point. Ensure the queue exists in Amazon SQS.");
    }

    public override async Task ProduceToTransport(object message, Type messageType, string path, IDictionary<string, object> messageHeaders, IMessageBusTarget targetBus, CancellationToken cancellationToken)
    {
        OnProduceToTransport(message, messageType, path, messageHeaders);

        var messageSerializer = GetMessageSerializer(path);
        var queueUrl = GetQueueUrlOrException(path);
        try
        {
            var (payload, attributes, deduplicationId, groupId) = GetTransportMessage(message, messageType, messageHeaders, messageSerializer);

            await _clientProvider.Client.SendMessageAsync(new SendMessageRequest(queueUrl, payload)
            {
                MessageAttributes = attributes,
                MessageDeduplicationId = deduplicationId,
                MessageGroupId = groupId
            }, cancellationToken);
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
            var messageSerializer = GetMessageSerializer(path);
            var queueUrl = GetQueueUrlOrException(path);

            var entries = new List<SendMessageBatchRequestEntry>(MaxMessagesInBatch);

            var envelopeChunks = envelopes.Chunk(MaxMessagesInBatch);
            foreach (var envelopeChunk in envelopeChunks)
            {
                foreach (var envelope in envelopeChunk)
                {
                    var (payload, attributes, deduplicationId, groupId) = GetTransportMessage(envelope.Message, envelope.MessageType, envelope.Headers, messageSerializer);

                    entries.Add(new SendMessageBatchRequestEntry(Guid.NewGuid().ToString(), payload)
                    {
                        MessageAttributes = attributes,
                        MessageDeduplicationId = deduplicationId,
                        MessageGroupId = groupId
                    });
                }

                await _clientProvider.Client.SendMessageBatchAsync(new SendMessageBatchRequest(queueUrl, entries), cancellationToken);

                entries.Clear();

                dispatched.AddRange(envelopeChunk);
            }

            return new(dispatched, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Producing message batch to path {Path} resulted in error {Error}", path, ex.Message);
            return new(dispatched, ex);
        }
    }

    private (string Payload, Dictionary<string, MessageAttributeValue> Attributes, string DeduplicationId, string GroupId) GetTransportMessage(object message, Type messageType, IDictionary<string, object> messageHeaders, IMessageSerializer<string> messageSerializer)
    {
        var producerSettings = GetProducerSettings(messageType);

        var messageDeduplicationIdProvider = producerSettings.GetOrDefault(SqsProperties.MessageDeduplicationId, null);
        var deduplicationId = messageDeduplicationIdProvider?.Invoke(message, messageHeaders);

        var messageGroupIdProvider = producerSettings.GetOrDefault(SqsProperties.MessageGroupId, null);
        var groupId = messageGroupIdProvider?.Invoke(message, messageHeaders);

        Dictionary<string, MessageAttributeValue> messageAttributes = null;
        if (messageHeaders != null)
        {
            messageAttributes = [];
            foreach (var header in messageHeaders)
            {
                var headerValue = HeaderSerializer.Serialize(header.Key, header.Value);
                messageAttributes.Add(header.Key, headerValue);
            }
        }

        var messagePayload = messageSerializer.Serialize(messageType, message);
        return (messagePayload, messageAttributes, deduplicationId, groupId);
    }
}