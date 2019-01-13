using Common.Logging;
using Microsoft.Azure.ServiceBus;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureServiceBus.Consumer
{
    public class TopicSubscriptionConsumer : BaseConsumer
    {
        private readonly SubscriptionClient _subscriptionClient;

        public TopicSubscriptionConsumer(ServiceBusMessageBus messageBus, ConsumerSettings consumerSettings) 
            : base(messageBus, consumerSettings,
                messageBus.ServiceBusSettings.SubscriptionClientFactory(new SubscriptionFactoryParams(consumerSettings.Topic, consumerSettings.GetSubscriptionName())),
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
