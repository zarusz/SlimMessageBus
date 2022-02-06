namespace SlimMessageBus.Host.AzureEventHub
{
    using Azure.Messaging.EventHubs;
    using SlimMessageBus.Host.Config;

    /// <summary>
    /// <see cref="EhPartitionConsumer"/> implementation meant for processing messages coming to consumers (<see cref="IConsumer{TMessage}"/>) in pub-sub or handlers (<see cref="IRequestHandler{TRequest,TResponse}"/>) in request-response flows.
    /// </summary>
    public class EhPartitionConsumerForConsumers : EhPartitionConsumer
    {
        private static void InitializeConsumerContext(EventData message, ConsumerContext consumerContext)
            => consumerContext.SetTransportMessage(message);

        public EhPartitionConsumerForConsumers(EventHubMessageBus messageBus, ConsumerSettings consumerSettings, PathGroup pathGroup, string partitionId)
            : base(messageBus, consumerSettings, new ConsumerInstanceMessageProcessor<EventData>(consumerSettings, messageBus, GetMessageWithHeaders, InitializeConsumerContext), pathGroup, partitionId)
        {
        }
    }
}