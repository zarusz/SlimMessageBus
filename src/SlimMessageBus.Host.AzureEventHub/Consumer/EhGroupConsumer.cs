namespace SlimMessageBus.Host.AzureEventHub;

using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Processor;

public class EhGroupConsumer : AbstractConsumer
{
    private readonly EventProcessorClient _processorClient;
    private readonly SafeDictionaryWrapper<string, EhPartitionConsumer> _partitionConsumerByPartitionId;
    private readonly GroupPath _groupPath;

    public EventHubMessageBus MessageBus { get; }

    public EhGroupConsumer(EventHubMessageBus messageBus, GroupPath groupPath, Func<GroupPathPartitionId, EhPartitionConsumer> partitionConsumerFactory)
        : base(messageBus.LoggerFactory.CreateLogger<EhGroupConsumer>())
    {
        _groupPath = groupPath ?? throw new ArgumentNullException(nameof(groupPath));
        if (partitionConsumerFactory == null) throw new ArgumentNullException(nameof(partitionConsumerFactory));

        MessageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));

        _partitionConsumerByPartitionId = new SafeDictionaryWrapper<string, EhPartitionConsumer>(partitionId =>
        {
            Logger.LogDebug("Creating PartitionConsumer for Group: {Group}, Path: {Path}, PartitionId: {PartitionId}", groupPath.Group, groupPath.Path, partitionId);
            try
            {
                return partitionConsumerFactory(new GroupPathPartitionId(groupPath, partitionId));
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error creating PartitionConsumer for Group: {Group}, Path: {Path}, PartitionId: {PartitionId}", groupPath.Group, groupPath.Path, partitionId);
                throw;
            }
        });

        Logger.LogInformation("Creating EventProcessorClient for EventHub with Group: {Group}, Path: {Path}", groupPath.Group, groupPath.Path);
        _processorClient = MessageBus.ProviderSettings.EventHubProcessorClientFactory(new ConsumerParams(groupPath.Path, groupPath.Group, messageBus.BlobContainerClient));
        _processorClient.PartitionInitializingAsync += PartitionInitializingAsync;
        _processorClient.PartitionClosingAsync += PartitionClosingAsync;
        _processorClient.ProcessEventAsync += ProcessEventHandler;
        _processorClient.ProcessErrorAsync += ProcessErrorHandler;
    }

    protected override async Task OnStart()
    {
        if (!_processorClient.IsRunning)
        {
            Logger.LogInformation("Starting consumer Group: {Group}, Path: {Path}...", _groupPath.Group, _groupPath.Path);
            await _processorClient.StartProcessingAsync().ConfigureAwait(false);
        }
    }

    protected override async Task OnStop()
    {
        if (_processorClient.IsRunning)
        {
            Logger.LogInformation("Stopping consumer Group: {Group}, Path: {Path}...", _groupPath.Group, _groupPath.Path);

            // stop the processing host
            await _processorClient.StopProcessingAsync().ConfigureAwait(false);

            var partitionConsumers = _partitionConsumerByPartitionId.ClearAndSnapshot();

            if (MessageBus.ProviderSettings.EnableCheckpointOnBusStop)
            {
                // checkpoint anything we've processed thus far
                await Task.WhenAll(partitionConsumers.Select(pc => pc.TryCheckpoint()));
            }
        }
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
        if (partitionId != null)
        {
            var ep = _partitionConsumerByPartitionId.GetOrAdd(partitionId);
            return ep.ProcessErrorAsync(args);
        }
        else
        {
            Logger.LogError(args.Exception, "Group error - Group: {Group}, Path: {Path}, Operation: {Operation}", _groupPath.Group, _groupPath.Path, args.Operation);
        }
        return Task.CompletedTask;
    }
}