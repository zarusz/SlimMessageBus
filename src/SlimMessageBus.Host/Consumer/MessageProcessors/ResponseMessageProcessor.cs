namespace SlimMessageBus.Host
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using SlimMessageBus.Host.Config;

    /// <summary>
    /// The <see cref="IMessageProcessor{TMessage}"/> implementation that processes the responses arriving to the bus.
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    public class ResponseMessageProcessor<TMessage> : IMessageProcessor<TMessage> where TMessage : class
    {
        private readonly ILogger<ResponseMessageProcessor<TMessage>> logger;
        private readonly RequestResponseSettings requestResponseSettings;
        private readonly MessageBusBase messageBus;
        private readonly Func<TMessage, MessageWithHeaders> messageProvider;

        public ResponseMessageProcessor(RequestResponseSettings requestResponseSettings, MessageBusBase messageBus, Func<TMessage, MessageWithHeaders> messageProvider)
        {
            if (messageBus is null) throw new ArgumentNullException(nameof(messageBus));
            
            this.logger = messageBus.LoggerFactory.CreateLogger<ResponseMessageProcessor<TMessage>>();
            this.requestResponseSettings = requestResponseSettings ?? throw new ArgumentNullException(nameof(requestResponseSettings));
            this.messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            this.messageProvider = messageProvider ?? throw new ArgumentNullException(nameof(messageProvider));
        }

        public AbstractConsumerSettings ConsumerSettings => requestResponseSettings;

        public Task<Exception> ProcessMessage(TMessage message, IMessageTypeConsumerInvokerSettings consumerInvoker)
        {
           try
            {
                var messageWithHeaders = messageProvider(message);
                return messageBus.OnResponseArrived(messageWithHeaders.Payload, requestResponseSettings.Path, messageWithHeaders.Headers);
            }
            catch (Exception e)
            {
                if (logger.IsEnabled(LogLevel.Error))
                {
                    logger.LogError(e, "Error occured while consuming response message, {Message}", message);
                }

                // We can only continue and process all messages in the lease    

                if (requestResponseSettings.OnResponseMessageFault != null)
                {
                    // Call the hook
                    logger.LogDebug("Executing the attached hook from {0}", nameof(requestResponseSettings.OnResponseMessageFault));
                    requestResponseSettings.OnResponseMessageFault(requestResponseSettings, message, e);
                }
            }
            return Task.FromResult<Exception>(null);
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