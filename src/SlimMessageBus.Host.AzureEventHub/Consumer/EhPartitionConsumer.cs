namespace SlimMessageBus.Host.AzureEventHub;

using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Processor;

public abstract class EhPartitionConsumer
{
    private readonly ILogger _logger;
    private ProcessEventArgs _lastMessage;
    private ProcessEventArgs _lastCheckpointMessage;

    protected EventHubMessageBus MessageBus { get; }
    protected IMessageProcessor<EventData> MessageProcessor { get; set; }
    protected ICheckpointTrigger CheckpointTrigger { get; set; }
    public GroupPathPartitionId GroupPathPartition { get; }

    protected EhPartitionConsumer(EventHubMessageBus messageBus, GroupPathPartitionId groupPathPartition)
    {
        MessageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
        GroupPathPartition = groupPathPartition ?? throw new ArgumentNullException(nameof(groupPathPartition));
        _logger = messageBus.LoggerFactory.CreateLogger<EhPartitionConsumer>();
    }

    public Task OpenAsync()
    {
        _logger.LogDebug("Open lease - Group: {Group}, Path: {Path}, PartitionId: {PartitionId}", GroupPathPartition.Group, GroupPathPartition.Path, GroupPathPartition.PartitionId);
        CheckpointTrigger.Reset();
        return Task.CompletedTask;
    }

    public Task CloseAsync(ProcessingStoppedReason reason)
    {
        _logger.LogDebug("Close lease - Reason: {Reason}, Group: {Group}, Path: {Path}, PartitionId: {PartitionId}", reason, GroupPathPartition.Group, GroupPathPartition.Path, GroupPathPartition.PartitionId);
        return Task.CompletedTask;
    }

    public async Task ProcessEventAsync(ProcessEventArgs args)
    {
        _logger.LogDebug("Message arrived - Group: {Group}, Path: {Path}, PartitionId: {PartitionId}, Offset: {Offset}", GroupPathPartition.Group, GroupPathPartition.Path, GroupPathPartition.PartitionId, args.Data.Offset);

        _lastMessage = args;

        var headers = GetHeadersFromTransportMessage(args.Data);
        var r = await MessageProcessor.ProcessMessage(args.Data, headers, cancellationToken: args.CancellationToken).ConfigureAwait(false);
        if (r.Exception != null)
        {
            // Note: The OnMessageFaulted was called at this point by the MessageProcessor.
            _logger.LogError(r.Exception, "Message error - Group: {Group}, Path: {Path}, PartitionId: {PartitionId}, Offset: {Offset}", GroupPathPartition.Group, GroupPathPartition.Path, GroupPathPartition.PartitionId, args.Data.Offset);

            // ToDo: Retry logic in the future
        }

        if (CheckpointTrigger.Increment() && !args.CancellationToken.IsCancellationRequested)
        {
            await Checkpoint(args).ConfigureAwait(false);
        }
    }

    public Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        // ToDo: add retry mechanism and dlq support
        _logger.LogError(args.Exception, "Partition error - Group: {Group}, Path: {Path}, PartitionId: {PartitionId}, Operation: {Operation}", GroupPathPartition.Group, GroupPathPartition.Path, GroupPathPartition.PartitionId, args.Operation);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Forces the checkpoint to happen if (the last message hasn't yet been checkpointed already).
    /// </summary>
    /// <returns></returns>
    private async Task Checkpoint(ProcessEventArgs args)
    {
        if (args.HasEvent && (!_lastCheckpointMessage.HasEvent || _lastCheckpointMessage.Data.Offset < args.Data.Offset))
        {
            _logger.LogDebug("Checkpoint at Group: {Group}, Path: {Path}, PartitionId: {PartitionId}, Offset: {Offset}", GroupPathPartition.Group, GroupPathPartition.Path, GroupPathPartition.PartitionId, args.Data.Offset);
            await args.UpdateCheckpointAsync();

            CheckpointTrigger.Reset();

            _lastCheckpointMessage = args;
        }
    }

    public Task TryCheckpoint()
        => Checkpoint(_lastMessage);

    protected static IReadOnlyDictionary<string, object> GetHeadersFromTransportMessage(EventData e)
        // Note: Try to see if the Properties are already IReadOnlyDictionary or Dictionary prior allocating a new collection
        => e.Properties as IReadOnlyDictionary<string, object> ?? new Dictionary<string, object>(e.Properties);
}

