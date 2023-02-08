namespace SlimMessageBus.Host.AzureServiceBus.Consumer;

using Azure.Messaging.ServiceBus;

using Microsoft.Extensions.Logging;

using SlimMessageBus.Host.Config;

public class AsbQueueConsumer : AsbBaseConsumer
{
    public AsbQueueConsumer(ServiceBusMessageBus messageBus, IMessageProcessor<ServiceBusReceivedMessage> messageProcessor, IEnumerable<AbstractConsumerSettings> consumerSettings, TopicSubscriptionParams topicSubscription, ServiceBusClient client)
        : base(messageBus ?? throw new ArgumentNullException(nameof(messageBus)),
              client,
              topicSubscription,
              messageProcessor,
              consumerSettings,
              messageBus.LoggerFactory.CreateLogger<AsbQueueConsumer>())
    {
    }
}