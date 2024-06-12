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

    protected override async Task<(IReadOnlyCollection<T> Dispatched, Exception Exception)> ProduceToTransport<T>(IReadOnlyCollection<T> envelopes, string path, IMessageBusTarget targetBus, CancellationToken cancellationToken = default)
    {
        AssertActive();

        var dispatched = new List<T>(envelopes.Count);
        try
        {
            var messages = envelopes
                .Where(x => x.Message != null)
                .Select(
                    envelope =>
                    {
                        var messageType = envelope.Message?.GetType();
                        var messagePayload = Serializer.Serialize(envelope.MessageType, envelope.Message);

                        _logger.LogDebug("Producing message {Message} of Type {MessageType} on Path {Path} with Size {MessageSize}", envelope.Message, messageType?.Name, path, messagePayload?.Length ?? 0);

                        var ev = envelope.Message != null ? new EventData(messagePayload) : new EventData();

                        if (envelope.Headers != null)
                        {
                            foreach (var header in envelope.Headers)
                            {
                                ev.Properties.Add(header.Key, header.Value);
                            }
                        }

                        var partitionKey = messageType != null
                            ? GetPartitionKey(messageType, envelope.Message)
                            : null;

                        return (Envelope: envelope, Message: ev, PartitionKey: partitionKey);
                    })
                    .GroupBy(x => x.PartitionKey);

            var producer = _producerByPath[path];
            foreach (var partition in messages)
            {
                EventDataBatch batch = null;
                try
                {
                    var items = partition.ToList();
                    if (items.Count == 1)
                    {
                        // only one item - quicker to send on its own
                        var item = items.Single();
                        await producer.SendAsync([item.Message], new SendEventOptions { PartitionKey = partition.Key }, cancellationToken);

                        dispatched.Add(item.Envelope);
                        continue;
                    }

                    // multiple items - send in batches
                    var inBatch = new List<T>(items.Count);
                    var i = 0;
                    while (i < items.Count)
                    {
                        var item = items[i];
                        batch ??= await producer.CreateBatchAsync(new CreateBatchOptions { PartitionKey = partition.Key }, cancellationToken);
                        if (batch.TryAdd(item.Message))
                        {
                            inBatch.Add(item.Envelope);
                            if (++i < items.Count)
                            {
                                continue;
                            }
                        }

                        if (batch.Count == 0)
                        {
                            throw new ProducerMessageBusException($"Failed to add message {item.Envelope.Message} of Type {item.Envelope.MessageType?.Name} on Path {path} to an empty batch");
                        }

                        await producer.SendAsync(batch, cancellationToken).ConfigureAwait(false);
                        dispatched.AddRange(inBatch);
                        inBatch.Clear();
                        batch.Dispose();
                        batch = null;
                    }

                    return (dispatched, null);
                }
                finally
                {
                    batch?.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            return (dispatched, ex);
        }

        return (dispatched, null);
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
