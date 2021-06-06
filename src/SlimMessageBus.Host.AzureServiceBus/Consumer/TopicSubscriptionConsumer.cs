namespace SlimMessageBus.Host.AzureServiceBus.Consumer
{
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Extensions.Logging;
    using SlimMessageBus.Host.Config;

    public class TopicSubscriptionConsumer : BaseConsumer
    {
        private readonly SubscriptionClient _subscriptionClient;

        public TopicSubscriptionConsumer(ServiceBusMessageBus messageBus, AbstractConsumerSettings consumerSettings, IMessageProcessor<Message> messageProcessor) 
            : base(messageBus, consumerSettings,
                messageBus.ProviderSettings.SubscriptionClientFactory(new SubscriptionFactoryParams(consumerSettings.Path, consumerSettings.GetSubscriptionName())),
                messageProcessor,
                messageBus.LoggerFactory.CreateLogger<TopicSubscriptionConsumer>())
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
