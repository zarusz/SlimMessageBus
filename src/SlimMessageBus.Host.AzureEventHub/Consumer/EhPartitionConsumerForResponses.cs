namespace SlimMessageBus.Host.AzureEventHub
{
    using Microsoft.Azure.EventHubs;
    using SlimMessageBus.Host.Config;

    /// <summary>
    /// <see cref="EhPartitionConsumer"/> implementation meant for processing responses returning back in the request-response flows.
    /// </summary>
    public class EhPartitionConsumerForResponses : EhPartitionConsumer
    {
        public EhPartitionConsumerForResponses(EventHubMessageBus messageBus, RequestResponseSettings requestResponseSettings, string partitionId)
            : base(messageBus, requestResponseSettings, new ResponseMessageProcessor<EventData>(requestResponseSettings, messageBus, GetMessageWithHeaders), partitionId)
        {
        }
    }
}