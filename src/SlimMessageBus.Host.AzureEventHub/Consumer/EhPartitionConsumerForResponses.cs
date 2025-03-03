namespace SlimMessageBus.Host.AzureEventHub;

using Azure.Messaging.EventHubs;

using SlimMessageBus.Host;

/// <summary>
/// <see cref="EhPartitionConsumer"/> implementation meant for processing responses returning back in the request-response flows.
/// </summary>
public class EhPartitionConsumerForResponses : EhPartitionConsumer
{
    public EhPartitionConsumerForResponses(
        EventHubMessageBus messageBus,
        RequestResponseSettings requestResponseSettings,
        GroupPathPartitionId pathGroupPartition,
        MessageProvider<EventData> messageProvider,
        IPendingRequestStore pendingRequestStore,
        TimeProvider timeProvider)
        : base(messageBus, pathGroupPartition)
    {
        if (requestResponseSettings == null) throw new ArgumentNullException(nameof(requestResponseSettings));

        MessageProcessor = new ResponseMessageProcessor<EventData>(MessageBus.LoggerFactory, requestResponseSettings, messageProvider, pendingRequestStore, timeProvider);
        CheckpointTrigger = new CheckpointTrigger(requestResponseSettings, MessageBus.LoggerFactory);
    }
}