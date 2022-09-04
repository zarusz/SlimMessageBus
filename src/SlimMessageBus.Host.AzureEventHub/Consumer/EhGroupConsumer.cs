namespace SlimMessageBus.Host.AzureEventHub;

using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Processor;
using SlimMessageBus.Host.Collections;

public class EhGroupConsumer : IAsyncDisposable, IConsumerControl
{
    private readonly ILogger _logger;
    private readonly EventProcessorClient _processorClient;
    private readonly SafeDictionaryWrapper<string, EhPartitionConsumer> _partitionConsumerByPartitionId;
    private readonly GroupPath _groupPath;
    public EventHubMessageBus MessageBus { get; }

    public EhGroupConsumer(EventHubMessageBus messageBus, GroupPath groupPath, Func<GroupPathPartitionId, EhPartitionConsumer> partitionConsumerFactory)
    {
        _groupPath = groupPath ?? throw new ArgumentNullException(nameof(groupPath));
        _ = partitionConsumerFactory ?? throw new ArgumentNullException(nameof(partitionConsumerFactory));

        MessageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
        _logger = messageBus.LoggerFactory.CreateLogger<EhGroupConsumer>();

        _partitionConsumerByPartitionId = new SafeDictionaryWrapper<string, EhPartitionConsumer>(partitionId =>
        {
            _logger.LogDebug("Creating PartitionConsumer for Group: {Group}, Path: {Path}, PartitionId: {PartitionId}", groupPath.Group, groupPath.Path, partitionId);
            try
            {
                return partitionConsumerFactory(new GroupPathPartitionId(groupPath, partitionId));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error creating PartitionConsumer for Group: {Group}, Path: {Path}, PartitionId: {PartitionId}", groupPath.Group, groupPath.Path, partitionId);
                throw;
            }
        });

        _logger.LogInformation("Creating EventProcessorClient for EventHub with Group: {Group}, Path: {Path}", groupPath.Group, groupPath.Path);
        _processorClient = MessageBus.ProviderSettings.EventHubProcessorClientFactory(new ConsumerParams(groupPath.Path, groupPath.Group, messageBus.BlobContainerClient));
        _processorClient.PartitionInitializingAsync += PartitionInitializingAsync;
        _processorClient.PartitionClosingAsync += PartitionClosingAsync;
        _processorClient.ProcessEventAsync += ProcessEventHandler;
        _processorClient.ProcessErrorAsync += ProcessErrorHandler;
    }

    private Task PartitionInitializingAsync(PartitionInitializingEventArgs args)
    {
        var partitionId = args.PartitionId;
        var ep = _partitionConsumerByPartitionId.GetOrAdd(partitionId);
        return ep.OpenAsync();
    }

    private Task PartitionClosingAsync(PartitionClosingEventArgs args)
    {
        var partitionId = args.PartitionId;
        var ep = _partitionConsumerByPartitionId.GetOrAdd(partitionId);
        return ep.CloseAsync(args.Reason);
    }

    private Task ProcessEventHandler(ProcessEventArgs args)
    {
        var partitionId = args.Partition.PartitionId;
        var ep = _partitionConsumerByPartitionId.GetOrAdd(partitionId);
        return ep.ProcessEventAsync(args);
    }

    private Task ProcessErrorHandler(ProcessErrorEventArgs args)
    {
        var partitionId = args.PartitionId;
        var ep = _partitionConsumerByPartitionId.GetOrAdd(partitionId);
        return ep.ProcessErrorAsync(args);
    }

    public async Task Start()
    {
        if (!_processorClient.IsRunning)
        {
            _logger.LogInformation("Starting consumer Group: {Group}, Path: {Path}...", _groupPath.Group, _groupPath.Path);
            await _processorClient.StartProcessingAsync();
        }
    }

    public async Task Stop()
    {
        if (_processorClient.IsRunning)
        {
            _logger.LogInformation("Stopping consumer Group: {Group}, Path: {Path}...", _groupPath.Group, _groupPath.Path);

            var partitionConsumers = _partitionConsumerByPartitionId.Snapshot();

            if (MessageBus.ProviderSettings.EnableCheckpointOnBusStop)
            {
                // checkpoint anything we've processed thus far
                await Task.WhenAll(partitionConsumers.Select(pc => pc.TryCheckpoint()));
            }

            // stop the processing host
            await _processorClient.StopProcessingAsync();
        }
    }

    #region IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        await Stop();

        _partitionConsumerByPartitionId.Clear();
    }

    #endregion
}