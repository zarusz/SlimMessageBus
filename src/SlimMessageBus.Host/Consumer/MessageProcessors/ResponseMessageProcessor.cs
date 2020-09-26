using System;
using System.Threading.Tasks;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host
{
    public class ResponseMessageProcessor<TMessage> : IMessageProcessor<TMessage> where TMessage : class
    {
        private readonly RequestResponseSettings _requestResponseSettings;
        private readonly MessageBusBase _messageBus;
        private readonly Func<TMessage, byte[]> _messagePayloadProvider;

        public ResponseMessageProcessor(RequestResponseSettings requestResponseSettings, MessageBusBase messageBus, Func<TMessage, byte[]> messagePayloadProvider)
        {
            _requestResponseSettings = requestResponseSettings ?? throw new ArgumentNullException(nameof(requestResponseSettings));
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _messagePayloadProvider = messagePayloadProvider ?? throw new ArgumentNullException(nameof(messagePayloadProvider));
        }

        public Task<Exception> ProcessMessage(TMessage message)
        {
            var payload = _messagePayloadProvider(message);
            return _messageBus.OnResponseArrived(payload, _requestResponseSettings.Topic);
        }

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}