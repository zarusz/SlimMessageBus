namespace SlimMessageBus.Host.AzureEventHub;

using Azure.Messaging.EventHubs;
using SlimMessageBus.Host.Config;

/// <summary>
/// <see cref="EhPartitionConsumer"/> implementation meant for processing responses returning back in the request-response flows.
/// </summary>
public class EhPartitionConsumerForResponses : EhPartitionConsumer
{
    private readonly RequestResponseSettings _requestResponseSettings;

    public EhPartitionConsumerForResponses(EventHubMessageBus messageBus, RequestResponseSettings requestResponseSettings, GroupPathPartitionId pathGroupPartition)
        : base(messageBus, pathGroupPartition)
    {
        _requestResponseSettings = requestResponseSettings ?? throw new ArgumentNullException(nameof(requestResponseSettings));

        MessageProcessor = new ResponseMessageProcessor<EventData>(_requestResponseSettings, MessageBus, messageProvider: eventData => eventData.EventBody.ToArray());
        CheckpointTrigger = new CheckpointTrigger(_requestResponseSettings, MessageBus.LoggerFactory);
    }
}