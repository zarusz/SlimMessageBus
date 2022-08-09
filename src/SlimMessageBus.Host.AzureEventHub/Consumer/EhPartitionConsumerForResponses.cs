namespace SlimMessageBus.Host.AzureEventHub;

using Azure.Messaging.EventHubs;
using SlimMessageBus.Host.Config;

/// <summary>
/// <see cref="EhPartitionConsumer"/> implementation meant for processing responses returning back in the request-response flows.
/// </summary>
public class EhPartitionConsumerForResponses : EhPartitionConsumer
{
    public EhPartitionConsumerForResponses(EventHubMessageBus messageBus, RequestResponseSettings requestResponseSettings, PathGroup pathGroup, string partitionId)
        : base(messageBus, requestResponseSettings, new ResponseMessageProcessor<EventData>(requestResponseSettings, messageBus, GetMessageWithHeaders), pathGroup, partitionId)
    {
    }
}