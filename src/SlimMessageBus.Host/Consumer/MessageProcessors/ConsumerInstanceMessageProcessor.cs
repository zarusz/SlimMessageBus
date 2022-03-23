namespace SlimMessageBus.Host
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using SlimMessageBus.Host.Config;

    /// <summary>
    /// Implementation of <see cref="IMessageProcessor{TMessage}"/> that peforms orchestration around processing of a new message using an instance of the declared consumer (<see cref="IConsumer{TMessage}"/> or <see cref="IRequestHandler{TRequest, TResponse}"/> interface).
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    public class ConsumerInstanceMessageProcessor<TMessage> : MessageHandler, IMessageProcessor<TMessage> where TMessage : class
    {
        private readonly ILogger logger;

        private readonly Func<TMessage, MessageWithHeaders> messageProvider;

        AbstractConsumerSettings IMessageProcessor<TMessage>.ConsumerSettings => ConsumerSettings;

        public ConsumerInstanceMessageProcessor(ConsumerSettings consumerSettings, MessageBusBase messageBus, Func<TMessage, MessageWithHeaders> messageProvider, Action<TMessage, ConsumerContext> consumerContextInitializer = null)
            : base(consumerSettings, messageBus ?? throw new ArgumentNullException(nameof(messageBus)), consumerContextInitializer == null ? null : (msg, ctx) => consumerContextInitializer((TMessage)msg, ctx))
        {
            logger = messageBus.LoggerFactory.CreateLogger<ConsumerInstanceMessageProcessor<TMessage>>();
            this.messageProvider = messageProvider ?? throw new ArgumentNullException(nameof(messageProvider));
        }

        #region IAsyncDisposable

        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        protected virtual ValueTask DisposeAsyncCore() => new();

        #endregion

        public virtual async Task<Exception> ProcessMessage(TMessage msg, IMessageTypeConsumerInvokerSettings consumerInvoker)
        {
            Exception responseException;
            try
            {
                var message = DeserializeMessage(msg, out var messageHeaders, consumerInvoker);

                object response = null;
                string requestId = null;

                (response, responseException, requestId) = await DoHandle(message, messageHeaders, nativeMessage: msg, consumerInvoker: consumerInvoker);

                if (ConsumerSettings.ConsumerMode == ConsumerMode.RequestResponse)
                {
                    await ProduceResponse(requestId, message, messageHeaders, response, responseException).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "Processing of the message {Message} of type {MessageType} failed", msg, ConsumerSettings.MessageType);
                responseException = e;
            }
            return responseException;
        }

        protected object DeserializeMessage(TMessage msg, out IDictionary<string, object> headers, IMessageTypeConsumerInvokerSettings invoker)
        {
            var messageWithHeaders = messageProvider(msg);

            headers = messageWithHeaders.Headers;

            logger.LogDebug("Deserializing message...");
            // ToDo: Take message type from header
            var messageType = invoker?.MessageType ?? ConsumerSettings.MessageType;
            var message = messageBus.Serializer.Deserialize(messageType, messageWithHeaders.Payload);

            return message;
        }

        private async Task ProduceResponse(string requestId, object request, IDictionary<string, object> requestHeaders, object response, Exception responseException)
        {
            // send the response (or error response)
            logger.LogDebug("Serializing the response {Response} of type {MessageType} for RequestId: {RequestId}...", response, ConsumerSettings.ResponseType, requestId);

            var responseHeaders = messageBus.CreateHeaders();
            responseHeaders.SetHeader(ReqRespMessageHeaders.RequestId, requestId);
            if (responseException != null)
            {
                responseHeaders.SetHeader(ReqRespMessageHeaders.Error, responseException.Message);
            }
            await messageBus.ProduceResponse(request, requestHeaders, response, responseHeaders, ConsumerSettings).ConfigureAwait(false);
        }
    }
}