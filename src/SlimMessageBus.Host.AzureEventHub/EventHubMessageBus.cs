namespace SlimMessageBus.Host.AzureEventHub;

using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Azure.Storage.Blobs;

using SlimMessageBus.Host;
using SlimMessageBus.Host.Collections;

/// <summary>
/// MessageBus implementation for Azure Event Hub.
/// </summary>
public class EventHubMessageBus : MessageBusBase
{
    private readonly ILogger _logger;
    private BlobContainerClient _blobContainerClient;
    private SafeDictionaryWrapper<string, EventHubProducerClient> _producerByPath;
    private List<EhGroupConsumer> _groupConsumers;

    protected internal BlobContainerClient BlobContainerClient => _blobContainerClient;

    public EventHubMessageBusSettings ProviderSettings { get; }

    public EventHubMessageBus(MessageBusSettings settings, EventHubMessageBusSettings eventHubSettings)
        : base(settings)
    {
        _logger = LoggerFactory.CreateLogger<EventHubMessageBus>();
        ProviderSettings = eventHubSettings;

        OnBuildProvider();
    }

    protected override void AssertSettings()
    {
        base.AssertSettings();

        if (string.IsNullOrEmpty(ProviderSettings.ConnectionString))
        {
            throw new ConfigurationMessageBusException(Settings, $"The {nameof(EventHubMessageBusSettings)}.{nameof(EventHubMessageBusSettings.ConnectionString)} must be set");
        }

        if (IsAnyConsumerDeclared)
        {
            if (string.IsNullOrEmpty(ProviderSettings.StorageConnectionString))
            {
                throw new ConfigurationMessageBusException(Settings, $"When consumers are declared, the {nameof(EventHubMessageBusSettings)}.{nameof(EventHubMessageBusSettings.StorageConnectionString)} must be set");
            }
            if (string.IsNullOrEmpty(ProviderSettings.StorageBlobContainerName))
            {
                throw new ConfigurationMessageBusException(Settings, $"When consumers are declared, the {nameof(EventHubMessageBusSettings)}.{nameof(EventHubMessageBusSettings.StorageBlobContainerName)} must be set");
            }
        }
    }

    private bool IsAnyConsumerDeclared => Settings.Consumers.Count > 0 || Settings.RequestResponse != null;

    #region Overrides of MessageBusBase

    protected override void Build()
    {
        base.Build();

        // Initialize storage client only when there are consumers declared
        _blobContainerClient = IsAnyConsumerDeclared
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

        _groupConsumers = new List<EhGroupConsumer>();

        _logger.LogInformation("Creating consumers");
        foreach (var (groupPath, consumerSettings) in Settings.Consumers.GroupBy(x => new GroupPath(path: x.Path, group: x.GetGroup())).ToDictionary(x => x.Key, x => x.ToList()))
        {
            _logger.LogInformation("Creating consumer for Path: {Path}, Group: {Group}", groupPath.Path, groupPath.Group);
            _groupConsumers.Add(new EhGroupConsumer(this, groupPath, groupPathPartition => new EhPartitionConsumerForConsumers(this, consumerSettings, groupPathPartition)));
        }

        if (Settings.RequestResponse != null)
        {
            var pathGroup = new GroupPath(Settings.RequestResponse.Path, Settings.RequestResponse.GetGroup());
            _logger.LogInformation("Creating response consumer for Path: {Path}, Group: {Group}", pathGroup.Path, pathGroup.Group);
            _groupConsumers.Add(new EhGroupConsumer(this, pathGroup, groupPathPartition => new EhPartitionConsumerForResponses(this, Settings.RequestResponse, groupPathPartition)));
        }
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        await base.DisposeAsyncCore();

        if (_groupConsumers != null)
        {
            foreach (var groupConsumer in _groupConsumers)
            {
                await groupConsumer.DisposeSilently("Consumer", _logger);
            }
            _groupConsumers.Clear();
            _groupConsumers = null;
        }

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

        await Task.WhenAll(_groupConsumers.Select(x => x.Start()));
    }

    protected override async Task OnStop()
    {
        await base.OnStop();
        await Task.WhenAll(_groupConsumers.Select(x => x.Stop()));
    }

    public override async Task ProduceToTransport(object message, string path, byte[] messagePayload, IDictionary<string, object> messageHeaders, CancellationToken cancellationToken)
    {
        AssertActive();

        var messageType = message?.GetType();

        _logger.LogDebug("Producing message {Message} of Type {MessageType} on Path {Path} with Size {MessageSize}", message, messageType?.Name, path, messagePayload?.Length ?? 0);

        var ev = messagePayload != null ? new EventData(messagePayload) : new EventData();

        if (messageHeaders != null)
        {
            foreach (var header in messageHeaders)
            {
                ev.Properties.Add(header.Key, header.Value);
            }
        }

        var partitionKey = messageType != null
            ? GetPartitionKey(messageType, message)
            : null;

        var producer = _producerByPath[path];

        // ToDo: Introduce some micro batching of events (store them between invocations and send when time span elapsed)
        using var eventBatch = await producer.CreateBatchAsync(new CreateBatchOptions { PartitionKey = partitionKey }, cancellationToken);

        if (!eventBatch.TryAdd(ev))
        {
            throw new ProducerMessageBusException($"Could not add message {message} of Type {messageType?.Name} on Path {path} to the send batch");
        }

        await producer.SendAsync(eventBatch, cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Delivered message {Message} of Type {MessageType} on Path {Path} with PartitionKey {PartitionKey}", message, messageType?.Name, path, partitionKey);
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
