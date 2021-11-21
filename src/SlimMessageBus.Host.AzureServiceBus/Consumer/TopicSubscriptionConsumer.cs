namespace SlimMessageBus.Host.AzureServiceBus.Consumer
{
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;

    public class TopicSubscriptionConsumer : BaseConsumer
    {
        private readonly SubscriptionClient subscriptionClient;

        public TopicSubscriptionConsumer(ServiceBusMessageBus messageBus, IEnumerable<IMessageProcessor<Message>> consumers, string path, string subscriptionName)
            : base(messageBus ?? throw new ArgumentNullException(nameof(messageBus)),
                messageBus.ProviderSettings.SubscriptionClientFactory(new SubscriptionFactoryParams(path, subscriptionName)),
                consumers,
                $"{path}/{subscriptionName}",
                subscriptionName: subscriptionName,
                messageBus.LoggerFactory.CreateLogger<TopicSubscriptionConsumer>())
        {
            subscriptionClient = (SubscriptionClient)Client;
        }

        #region IDisposable

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                subscriptionClient.CloseAsync().GetAwaiter().GetResult();
            }
            base.Dispose(disposing);
        }

        #endregion           
    }
}
