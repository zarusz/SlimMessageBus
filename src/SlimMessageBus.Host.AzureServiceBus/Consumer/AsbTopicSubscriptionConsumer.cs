namespace SlimMessageBus.Host.AzureServiceBus.Consumer;

using Azure.Messaging.ServiceBus;

using SlimMessageBus.Host.Config;

public class AsbTopicSubscriptionConsumer : AsbBaseConsumer
{
    public AsbTopicSubscriptionConsumer(ServiceBusMessageBus messageBus, IMessageProcessor<ServiceBusReceivedMessage> messageProcessor, IEnumerable<AbstractConsumerSettings> consumerSettings, TopicSubscriptionParams topicSubscription, ServiceBusClient client)
        : base(messageBus ?? throw new ArgumentNullException(nameof(messageBus)),
            client,
            topicSubscription,
            messageProcessor,
            consumerSettings,
            messageBus.LoggerFactory.CreateLogger<AsbTopicSubscriptionConsumer>())
    {
    }
}
