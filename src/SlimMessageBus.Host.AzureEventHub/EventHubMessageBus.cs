namespace SlimMessageBus.Host.AzureEventHub;

using SlimMessageBus.Host.Services;

/// <summary>
/// MessageBus implementation for Azure Event Hub.
/// </summary>
public class EventHubMessageBus : MessageBusBase<EventHubMessageBusSettings>
{
    private readonly ILogger _logger;
    private BlobContainerClient _blobContainerClient;
    private SafeDictionaryWrapper<string, EventHubProducerClient> _producerByPath;

    protected internal BlobContainerClient BlobContainerClient => _blobContainerClient;

    public EventHubMessageBus(MessageBusSettings settings, EventHubMessageBusSettings providerSettings)
        : base(settings, providerSettings)
    {
        _logger = LoggerFactory.CreateLogger<EventHubMessageBus>();

        OnBuildProvider();
    }

    public override int? MaxMessagesPerTransaction => 100;

    protected override IMessageBusSettingsValidationService ValidationService => new EventHubMessageBusSettingsValidationService(Settings, ProviderSettings);

    #region Overrides of MessageBusBase

    protected override void Build()
    {
        base.Build();

        // Initialize storage client only when there are consumers declared
        _blobContainerClient = Settings.IsAnyConsumerDeclared()
            ? ProviderSettings.BlobContanerClientFactory()
            : null;

        _producerByPath = new SafeDictionaryWrapper<string, EventHubProducerClient>(path =>
        {
            _logger.LogDebug("Creating EventHubClient for path {Path}", path);
            try
            {
                return ProviderSettings.EventHubProducerClientFactory(path);
            }
            catch (Exception e)
            {
                _logger.LogDebug(e, "Error creating EventHubClient for path {Path}", path);
                throw;
            }
        });
    }

    protected override async Task CreateConsumers()
    {
        await base.CreateConsumers();

        foreach (var (groupPath, consumerSettings) in Settings.Consumers.GroupBy(x => new GroupPath(path: x.Path, group: x.GetGroup())).ToDictionary(x => x.Key, x => x.ToList()))
        {
            _logger.LogInformation("Creating consumer for Path: {Path}, Group: {Group}", groupPath.Path, groupPath.Group);
            AddConsumer(new EhGroupConsumer(this, groupPath, groupPathPartition => new EhPartitionConsumerForConsumers(this, consumerSettings, groupPathPartition)));
        }

        if (Settings.RequestResponse != null)
        {
            var pathGroup = new GroupPath(Settings.RequestResponse.Path, Settings.RequestResponse.GetGroup());
            _logger.LogInformation("Creating response consumer for Path: {Path}, Group: {Group}", pathGroup.Path, pathGroup.Group);
            AddConsumer(new EhGroupConsumer(this, pathGroup, groupPathPartition => new EhPartitionConsumerForResponses(this, Settings.RequestResponse, groupPathPartition)));
        }

    }

    protected override async ValueTask DisposeAsyncCore()
    {
        await base.DisposeAsyncCore();

        if (_producerByPath != null)
        {
            var producers = _producerByPath.ClearAndSnapshot();
            foreach (var producer in producers)
            {
                _logger.LogDebug("Closing EventHubProducerClient for Path {Path}", producer.EventHubName);
                await producer.DisposeSilently();
            }
            _producerByPath = null;
        }
    }

    protected override async Task OnStart()
    {
        await base.OnStart();

        if (_blobContainerClient != null)
        {
            // Create blob storage container if not exists
            try
            {
                await _blobContainerClient.CreateIfNotExistsAsync();
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Attempt to create blob container {BlobContainer} failed - the blob container is needed to store the consumer group offsets", _blobContainerClient.Name);
            }
        }
    }

    private EventData GetTransportMessage(object message, Type messageType, IDictionary<string, object> messageHeaders, string path, out string partitionKey)
    {
        OnProduceToTransport(message, messageType, path, messageHeaders);

        var messagePayload = message != null
            ? Serializer.Serialize(messageType, message)
            : null;

        var transportMessage = message != null
            ? new EventData(messagePayload)
            : new EventData();

        if (messageHeaders != null)
        {
            foreach (var header in messageHeaders)
            {
                transportMessage.Properties.Add(header.Key, header.Value);
            }
        }

        partitionKey = messageType != null
            ? GetPartitionKey(messageType, message)
            : null;

        return transportMessage;
    }

    public override async Task ProduceToTransport(object message, Type messageType, string path, IDictionary<string, object> messageHeaders, IMessageBusTarget targetBus, CancellationToken cancellationToken)
    {
        try
        {
            var transportMessage = GetTransportMessage(message, messageType, messageHeaders, path, out var partitionKey);
            var producer = _producerByPath[path];
            await producer.SendAsync([transportMessage], new SendEventOptions { PartitionKey = partitionKey }, cancellationToken);
        }
        catch (Exception ex) when (ex is not ProducerMessageBusException && ex is not TaskCanceledException)
        {
            throw new ProducerMessageBusException(GetProducerErrorMessage(path, message, messageType, ex), ex);
        }
    }

    public override async Task<ProduceToTransportBulkResult<T>> ProduceToTransportBulk<T>(IReadOnlyCollection<T> envelopes, string path, IMessageBusTarget targetBus, CancellationToken cancellationToken)
    {
        AssertActive();

        var dispatched = new List<T>(envelopes.Count);
        try
        {
            var producer = _producerByPath[path];

            var messagesByPartition = envelopes
                .Where(x => x.Message != null)
                .Select(envelope =>
                {
                    var transportMessage = GetTransportMessage(envelope.Message, envelope.MessageType, envelope.Headers, path, out var partitionKey);
                    return (Envelope: envelope, TransportMessage: transportMessage, PartitionKey: partitionKey);
                })
                .GroupBy(x => x.PartitionKey);

            var inBatch = new List<T>(envelopes.Count);
            foreach (var partition in messagesByPartition)
            {
                var batchOptions = new CreateBatchOptions { PartitionKey = partition.Key };
                EventDataBatch batch = null;
                try
                {
                    using var it = partition.GetEnumerator();
                    var advance = it.MoveNext();
                    while (advance)
                    {
                        var item = it.Current;

                        batch ??= await producer.CreateBatchAsync(batchOptions, cancellationToken);
                        if (batch.TryAdd(item.TransportMessage))
                        {
                            inBatch.Add(item.Envelope);
                            advance = it.MoveNext();
                            if (advance)
                            {
                                continue;
                            }
                        }

                        if (batch.Count == 0)
                        {
                            throw new ProducerMessageBusException($"Failed to add message {item.Envelope.Message} of type {item.Envelope.MessageType?.Name} on path {path} to an empty batch");
                        }

                        advance = false;
                        await producer.SendAsync(batch, cancellationToken).ConfigureAwait(false);
                        dispatched.AddRange(inBatch);
                        inBatch.Clear();

                        batch.Dispose();
                        batch = null;
                    }
                }
                finally
                {
                    batch?.Dispose();
                }
            }
            return new(dispatched, null);
        }
        catch (Exception ex)
        {
            return new(dispatched, ex);
        }
    }

    #endregion

    private string GetPartitionKey(Type messageType, object message)
    {
        var producerSettings = GetProducerSettings(messageType);
        try
        {
            var keyProvider = producerSettings?.GetKeyProvider();
            var partitionKey = keyProvider?.Invoke(message);
            return partitionKey;
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "The configured message KeyProvider failed for message type {MessageType} and message {Message}", messageType, message);
        }
        return null;
    }
}
