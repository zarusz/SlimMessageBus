namespace SlimMessageBus.Host.AzureServiceBus.Consumer
{
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;

    public class QueueConsumer : BaseConsumer
    {
        private readonly IQueueClient queueClient;

        public QueueConsumer(ServiceBusMessageBus messageBus, IEnumerable<IMessageProcessor<Message>> consumers, string path)
            : base(messageBus ?? throw new ArgumentNullException(nameof(messageBus)),
                  messageBus.ProviderSettings.QueueClientFactory(path),
                  consumers,
                  path,
                  subscriptionName: null,
                  messageBus.LoggerFactory.CreateLogger<QueueConsumer>())
        {
            queueClient = (IQueueClient)Client;
        }

        #region IDisposable

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                queueClient.CloseAsync().GetAwaiter().GetResult();
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}