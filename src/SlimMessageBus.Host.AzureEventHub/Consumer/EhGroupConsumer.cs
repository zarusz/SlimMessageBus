namespace SlimMessageBus.Host.AzureEventHub;

using System.Diagnostics.CodeAnalysis;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Processor;
using SlimMessageBus.Host.Collections;
using SlimMessageBus.Host.Config;

public class EhGroupConsumer : IAsyncDisposable, IConsumerControl
{
    private readonly ILogger logger;

    public EventHubMessageBus MessageBus { get; }

    private readonly EventProcessorClient processorClient;
    private readonly SafeDictionaryWrapper<string, EhPartitionConsumer> partitionConsumerByPartitionId;
    private readonly PathGroup pathGroup;

    public EhGroupConsumer(EventHubMessageBus messageBus, [NotNull] ConsumerSettings consumerSettings)
        : this(messageBus, new PathGroup(consumerSettings.Path, consumerSettings.GetGroup()), (pathGroup, partitionId) => new EhPartitionConsumerForConsumers(messageBus, consumerSettings, pathGroup, partitionId))
    {
    }

    public EhGroupConsumer(EventHubMessageBus messageBus, [NotNull] RequestResponseSettings requestResponseSettings)
        : this(messageBus, new PathGroup(requestResponseSettings.Path, requestResponseSettings.GetGroup()), (pathGroup, partitionId) => new EhPartitionConsumerForResponses(messageBus, requestResponseSettings, pathGroup, partitionId))
    {
    }

    protected EhGroupConsumer(EventHubMessageBus messageBus, PathGroup pathGroup, Func<PathGroup, string, EhPartitionConsumer> partitionConsumerFactory)
    {
        this.pathGroup = pathGroup ?? throw new ArgumentNullException(nameof(pathGroup));
        _ = partitionConsumerFactory ?? throw new ArgumentNullException(nameof(partitionConsumerFactory));

        MessageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
        logger = messageBus.LoggerFactory.CreateLogger<EhGroupConsumer>();

        partitionConsumerByPartitionId = new SafeDictionaryWrapper<string, EhPartitionConsumer>(partitionId =>
        {
            logger.LogDebug("Creating PartitionConsumer for Group: {Group}, Path: {Path}, PartitionId: {PartitionId}", pathGroup.Group, pathGroup.Path, partitionId);
            return partitionConsumerFactory(pathGroup, partitionId);
        });

        logger.LogInformation("Creating EventProcessorClient for EventHub with Group: {Group}, Path: {Path}", pathGroup.Group, pathGroup.Path);
        processorClient = MessageBus.ProviderSettings.EventHubProcessorClientFactory(new ConsumerParams(pathGroup.Path, pathGroup.Group, messageBus.BlobContainerClient));
        processorClient.PartitionInitializingAsync += PartitionInitializingAsync;
        processorClient.PartitionClosingAsync += PartitionClosingAsync;
        processorClient.ProcessEventAsync += ProcessEventHandler;
        processorClient.ProcessErrorAsync += ProcessErrorHandler;
    }

    private Task PartitionInitializingAsync(PartitionInitializingEventArgs args)
    {
        var partitionId = args.PartitionId;
        var ep = partitionConsumerByPartitionId.GetOrAdd(partitionId);
        return ep.OpenAsync();
    }

    private Task PartitionClosingAsync(PartitionClosingEventArgs args)
    {
        var partitionId = args.PartitionId;
        var ep = partitionConsumerByPartitionId.GetOrAdd(partitionId);
        return ep.CloseAsync(args.Reason);
    }

    private Task ProcessEventHandler(ProcessEventArgs args)
    {
        var partitionId = args.Partition.PartitionId;
        var ep = partitionConsumerByPartitionId.GetOrAdd(partitionId);
        return ep.ProcessEventAsync(args);
    }

    private Task ProcessErrorHandler(ProcessErrorEventArgs args)
    {
        var partitionId = args.PartitionId;
        var ep = partitionConsumerByPartitionId.GetOrAdd(partitionId);
        return ep.ProcessErrorAsync(args);
    }

    public async Task Start()
    {
        if (!processorClient.IsRunning)
        {
            logger.LogInformation("Starting consumer Group: {Group}, Path: {Path}...", pathGroup.Group, pathGroup.Path);
            await processorClient.StartProcessingAsync();
        }
    }

    public async Task Stop()
    {
        if (processorClient.IsRunning)
        {
            logger.LogInformation("Stopping consumer Group: {Group}, Path: {Path}...", pathGroup.Group, pathGroup.Path);

            var partitionConsumers = partitionConsumerByPartitionId.Snapshot();

            if (MessageBus.ProviderSettings.EnableCheckpointOnBusStop)
            {
                // checkpoint anything we've processed thus far
                await Task.WhenAll(partitionConsumers.Select(pc => pc.TryCheckpoint()));
            }

            // stop the processing host
            await processorClient.StopProcessingAsync();
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

        partitionConsumerByPartitionId.Clear();
    }

    #endregion
}