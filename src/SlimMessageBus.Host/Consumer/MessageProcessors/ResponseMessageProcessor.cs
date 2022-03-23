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
                logger.LogError(e, "Error occured while consuming response message, {Message}", message);

                if (requestResponseSettings.OnResponseMessageFault != null)
                {
                    // Call the hook
                    try
                    {
                        requestResponseSettings.OnResponseMessageFault(requestResponseSettings, message, e);
                    }
                    catch (Exception eh)
                    {
                        MessageBusBase.HookFailed(logger, eh, nameof(IConsumerEvents.OnMessageFault));
                    }
                }

                // We can only continue and process all messages in the lease    
                return Task.FromResult<Exception>(null);
            }
        }

        #region IAsyncDisposable

        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        protected virtual ValueTask DisposeAsyncCore() => new();

        #endregion
    }
}