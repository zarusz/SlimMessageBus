using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureServiceBus.Consumer
{
    public class ResponseMessageProcessor : IMessageProcessor<Message>
    {
        private readonly RequestResponseSettings _requestResponseSettings;
        private readonly MessageBusBase _messageBus;

        public ResponseMessageProcessor(RequestResponseSettings requestResponseSettings, MessageBusBase messageBus)
        {
            _requestResponseSettings = requestResponseSettings;
            _messageBus = messageBus;
        }

        #region IDisposable

        public void Dispose()
        {
        }

        #endregion

        public Task ProcessMessage(Message message)
        {
            return _messageBus.OnResponseArrived(message.Body, _requestResponseSettings.Topic);
        }
    }
}