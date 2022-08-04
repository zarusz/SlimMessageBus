namespace SlimMessageBus.Host.AzureServiceBus.Consumer;

using Azure.Messaging.ServiceBus;

public class AsbTopicSubscriptionConsumer : AsbBaseConsumer
{
    public AsbTopicSubscriptionConsumer(ServiceBusMessageBus messageBus, IEnumerable<IMessageProcessor<ServiceBusReceivedMessage>> consumers, TopicSubscriptionParams topicSubscription, ServiceBusClient client)
        : base(messageBus ?? throw new ArgumentNullException(nameof(messageBus)),
            client,
            topicSubscription,
            consumers,
            messageBus.LoggerFactory.CreateLogger<AsbTopicSubscriptionConsumer>())
    {
    }
}
