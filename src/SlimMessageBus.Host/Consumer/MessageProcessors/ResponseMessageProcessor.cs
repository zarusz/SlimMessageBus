namespace SlimMessageBus.Host
{
    using System;
    using System.Threading.Tasks;
    using SlimMessageBus.Host.Config;

    /// <summary>
    /// The <see cref="IMessageProcessor{TMessage}"/> implementation that processes the responses arriving to the bus.
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    public class ResponseMessageProcessor<TMessage> : IMessageProcessor<TMessage> where TMessage : class
    {
        private readonly RequestResponseSettings _requestResponseSettings;
        private readonly MessageBusBase _messageBus;
        private readonly Func<TMessage, MessageWithHeaders> _messageProvider;

        public ResponseMessageProcessor(RequestResponseSettings requestResponseSettings, MessageBusBase messageBus, Func<TMessage, MessageWithHeaders> messageProvider)
        {
            _requestResponseSettings = requestResponseSettings ?? throw new ArgumentNullException(nameof(requestResponseSettings));
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _messageProvider = messageProvider ?? throw new ArgumentNullException(nameof(messageProvider));
        }

        public Task<Exception> ProcessMessage(TMessage message)
        {
            var messageWithHeaders = _messageProvider(message);
            return _messageBus.OnResponseArrived(messageWithHeaders.Payload, _requestResponseSettings.Path, messageWithHeaders.Headers);
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