using Common.Logging;
using Microsoft.Azure.ServiceBus;
using SlimMessageBus.Host.Config;
using System;

namespace SlimMessageBus.Host.AzureServiceBus.Consumer
{
    public class QueueConsumer : BaseConsumer
    {
        private readonly IQueueClient _queueClient;

        public QueueConsumer(ServiceBusMessageBus messageBus, AbstractConsumerSettings consumerSettings, IMessageProcessor<Message> messageProcessor) 
            : base(messageBus ?? throw new ArgumentNullException(nameof(messageBus)),
                  consumerSettings ?? throw new ArgumentNullException(nameof(consumerSettings)),
                  messageBus.ProviderSettings.QueueClientFactory(consumerSettings.Topic),
                  messageProcessor,
                  LogManager.GetLogger<QueueConsumer>())
        {
            _queueClient = (IQueueClient) Client;
        }

        #region IDisposable

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _queueClient.CloseAsync().GetAwaiter().GetResult();
            }
        }

        #endregion
    }
}