using Common.Logging;
using Microsoft.Azure.ServiceBus;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureServiceBus.Consumer
{
    public class TopicSubscriptionConsumer : BaseConsumer
    {
        private readonly SubscriptionClient _subscriptionClient;

        public TopicSubscriptionConsumer(ServiceBusMessageBus messageBus, AbstractConsumerSettings consumerSettings, IMessageProcessor<Message> messageProcessor) 
            : base(messageBus, consumerSettings,
                messageBus.ServiceBusSettings.SubscriptionClientFactory(new SubscriptionFactoryParams(consumerSettings.Topic, consumerSettings.GetSubscriptionName())),
                messageProcessor,
                LogManager.GetLogger<TopicSubscriptionConsumer>())
        {
            _subscriptionClient = (SubscriptionClient) Client;
        }

        #region IDisposable

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _subscriptionClient.CloseAsync().GetAwaiter().GetResult();
            }
        }

        #endregion           
    }
}
