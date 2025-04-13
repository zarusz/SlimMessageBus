namespace SlimMessageBus.Host.AmazonSQS;

using Amazon.SimpleNotificationService.Model;

public class SqsMessageBus : MessageBusBase<SqsMessageBusSettings>
{
    private readonly ILogger _logger;

    private readonly ISqsClientProvider _clientProviderSqs;
    private readonly ISnsClientProvider _clientProviderSns;

    private readonly Dictionary<string, (string Url, PathKind PathKind)> _urlKindByPath = [];

    public ISqsHeaderSerializer<Amazon.SQS.Model.MessageAttributeValue> SqsHeaderSerializer { get; }
    public ISqsHeaderSerializer<Amazon.SimpleNotificationService.Model.MessageAttributeValue> SnsHeaderSerializer { get; }

    public SqsMessageBus(MessageBusSettings settings, SqsMessageBusSettings providerSettings)
        : base(settings, providerSettings)
    {
        _logger = LoggerFactory.CreateLogger<SqsMessageBus>();

        _clientProviderSqs = settings.ServiceProvider.GetRequiredService<ISqsClientProvider>();
        _clientProviderSns = settings.ServiceProvider.GetRequiredService<ISnsClientProvider>();

        SqsHeaderSerializer = providerSettings.SqsHeaderSerializer;
        SnsHeaderSerializer = providerSettings.SnsHeaderSerializer;

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
                var consumer = new SqsQueueConsumer(this, path, _clientProviderSqs, messageProcessor, consumerSettings);
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
            await _clientProviderSqs.EnsureClientAuthenticated();
            await _clientProviderSns.EnsureClientAuthenticated();

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

        var provisioningService = new SqsTopologyService(LoggerFactory.CreateLogger<SqsTopologyService>(), Settings, ProviderSettings, _clientProviderSqs, _clientProviderSns);
        await provisioningService.ProvisionTopology(CancellationToken); // provisioning happens asynchronously
    }

    private async Task PopulatePathToUrlMappings()
    {
        var paths = Settings.Producers.Select(x => (Path: x.DefaultPath, x.PathKind))
            .Concat(Settings.Consumers.Select(x => (x.Path, x.PathKind)))
            .Concat(Settings.RequestResponse is not null ? [(Settings.RequestResponse.Path, Settings.RequestResponse.PathKind)] : [])
            .ToHashSet();

        var kindByPath = new Dictionary<string, PathKind>(paths.Count);

        // Ensure a path (queue/topic) have overlapping names
        foreach (var (path, pathKind) in paths)
        {
            if (kindByPath.TryGetValue(path, out var existingKind))
            {
                if (existingKind != pathKind)
                {
                    throw new ConfigurationMessageBusException($"Path {path} is declared as both {existingKind} and {pathKind}");
                }
            }
            else
            {
                kindByPath[path] = pathKind;
            }
        }

        // Ensure the queue/topic exists and we have their URL for SQS queues or ARN for SNS topics
        foreach (var (path, pathKind) in kindByPath)
        {
            string url;

            if (pathKind == PathKind.Queue)
            {
                try
                {
                    _logger.LogDebug("Populating URL for queue {QueueName}", path);
                    var queueResponse = await _clientProviderSqs.Client.GetQueueUrlAsync(path, CancellationToken);
                    url = queueResponse.QueueUrl;
                }
                catch (QueueDoesNotExistException)
                {
                    throw new ConfigurationMessageBusException($"Queue {path} does not exist");
                }
            }
            else
            {
                _logger.LogDebug("Populating URL for topic {TopicName}", path);
                var topicResponse = await _clientProviderSns.Client.FindTopicAsync(path)
                    ?? throw new ConfigurationMessageBusException($"Topic {path} does not exist");

                url = topicResponse.TopicArn;
            }

            _urlKindByPath[path] = (url, pathKind);
        }
    }

    internal (string Url, PathKind PathKind) GetUrlAndKindOrException(string path)
    {
        if (_urlKindByPath.TryGetValue(path, out var urlAndKind))
        {
            return urlAndKind;
        }
        throw new ProducerMessageBusException($"The {path} has unknown URL at this point. Ensure the queue exists in Amazon SQS/SNS and the queue or topic is declared in SMB.");
    }

    public override async Task ProduceToTransport(object message, Type messageType, string path, IDictionary<string, object> messageHeaders, IMessageBusTarget targetBus, CancellationToken cancellationToken)
    {
        OnProduceToTransport(message, messageType, path, messageHeaders);

        var messageSerializer = GetMessageSerializer(path);

        var (url, pathKind) = GetUrlAndKindOrException(path);

        try
        {

            if (pathKind == PathKind.Queue)
            {
                var (payload, attributes, deduplicationId, groupId) = GetTransportMessage(message, messageType, messageHeaders, messageSerializer, SqsHeaderSerializer);
                await _clientProviderSqs.Client.SendMessageAsync(new SendMessageRequest(url, payload)
                {
                    MessageAttributes = attributes,
                    MessageDeduplicationId = deduplicationId,
                    MessageGroupId = groupId
                }, cancellationToken);
            }
            else
            {
                var (payload, attributes, deduplicationId, groupId) = GetTransportMessage(message, messageType, messageHeaders, messageSerializer, SnsHeaderSerializer);
                await _clientProviderSns.Client.PublishAsync(new PublishRequest(url, payload)
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
            var messageSerializer = GetMessageSerializer(path);
            var (url, pathKind) = GetUrlAndKindOrException(path);

            if (pathKind == PathKind.Queue)
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

                    await _clientProviderSqs.Client.SendMessageBatchAsync(new SendMessageBatchRequest(url, entries), cancellationToken);

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

                    await _clientProviderSns.Client.PublishBatchAsync(new PublishBatchRequest { TopicArn = url, PublishBatchRequestEntries = entries }, cancellationToken);

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