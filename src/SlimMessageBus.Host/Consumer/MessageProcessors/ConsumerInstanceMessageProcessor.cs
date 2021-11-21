namespace SlimMessageBus.Host
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using SlimMessageBus.Host.Config;
    using SlimMessageBus.Host.DependencyResolver;

    /// <summary>
    /// Implementation of <see cref="IMessageProcessor{TMessage}"/> that peforms orchestration around processing of a new message using an instance of the declared consumer (<see cref="IConsumer{TMessage}"/> or <see cref="IRequestHandler{TRequest, TResponse}"/> interface).
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    public class ConsumerInstanceMessageProcessor<TMessage> : IMessageProcessor<TMessage> where TMessage : class
    {
        private readonly ILogger logger;

        private readonly MessageBusBase messageBus;
        private readonly ConsumerSettings consumerSettings;

        private readonly Func<TMessage, MessageWithHeaders> messageProvider;

        private readonly bool createMessageScope;

        private readonly bool consumerWithContext;
        private readonly Action<TMessage, ConsumerContext> consumerContextInitializer;

        public ConsumerInstanceMessageProcessor(ConsumerSettings consumerSettings, MessageBusBase messageBus, Func<TMessage, MessageWithHeaders> messageProvider, Action<TMessage, ConsumerContext> consumerContextInitializer = null)
        {
            if (messageBus is null) throw new ArgumentNullException(nameof(messageBus));

            logger = messageBus.LoggerFactory.CreateLogger<ConsumerInstanceMessageProcessor<TMessage>>();
            this.consumerSettings = consumerSettings ?? throw new ArgumentNullException(nameof(consumerSettings));
            this.messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            this.messageProvider = messageProvider ?? throw new ArgumentNullException(nameof(messageProvider));

            createMessageScope = this.messageBus.IsMessageScopeEnabled(this.consumerSettings);

            this.consumerContextInitializer = consumerContextInitializer;
            consumerWithContext = typeof(IConsumerWithContext).IsAssignableFrom(consumerSettings.ConsumerType);
        }

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
            }
        }

        #endregion

        public AbstractConsumerSettings ConsumerSettings => consumerSettings;

        public virtual async Task<Exception> ProcessMessage(TMessage msg, IMessageTypeConsumerInvokerSettings consumerInvoker)
        {
            Exception exceptionResult = null;
            try
            {
                var message = DeserializeMessage(msg, out var messageHeaders, out var requestId, out var expires, consumerInvoker);

                // Verify if the request/message is already expired
                if (expires != null)
                {
                    var currentTime = messageBus.CurrentTime;
                    if (currentTime > expires.Value)
                    {
                        OnMessageExpired(expires, message, currentTime, msg);

                        // Do not process the expired message
                        return null;
                    }
                }

                object response = null;
                string responseError = null;

                if (createMessageScope)
                {
                    logger.LogDebug("Creating message scope for message {Message} of type {MessageType}", message, consumerSettings.MessageType);
                }

                var messageScope = createMessageScope
                    ? messageBus.Settings.DependencyResolver.CreateScope()
                    : messageBus.Settings.DependencyResolver;

                // Set MessageScope.Current, so any future integration might need to use that
                MessageScope.Current = messageScope;

                try
                {
                    OnMessageArrived(message, msg);

                    var consumerType = consumerInvoker?.ConsumerType ?? consumerSettings.ConsumerType;
                    var consumerInstance = messageScope.Resolve(consumerType)
                        ?? throw new ConfigurationMessageBusException($"Could not resolve consumer/handler type {consumerType} from the DI container. Please check that the configured type {consumerType} is registered within the DI container.");
                    try
                    {
                        response = await ExecuteConsumer(msg, message, messageHeaders, consumerInstance, consumerInvoker).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        responseError = OnMessageError(message, e, msg);
                        exceptionResult = e;
                    }
                    finally
                    {
                        OnMessageFinished(message, msg);

                        if (consumerSettings.IsDisposeConsumerEnabled && consumerInstance is IDisposable consumerInstanceDisposable)
                        {
                            logger.LogDebug("Disposing consumer instance {Consumer} of type {ConsumerType}", consumerInstance, consumerType);
                            consumerInstanceDisposable.DisposeSilently("ConsumerInstance", logger);
                        }
                    }
                }
                finally
                {
                    // Clear the MessageScope.Current
                    MessageScope.Current = null;

                    if (createMessageScope)
                    {
                        logger.LogDebug("Disposing message scope for message {Message} of type {MessageType}", message, consumerSettings.MessageType);
                        ((IChildDependencyResolver)messageScope).DisposeSilently("Scope", logger);
                    }
                }

                if (response != null || responseError != null)
                {
                    await ProduceResponse(requestId, message, messageHeaders, response, responseError).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "Processing of the message {Message} of type {MessageType} failed", msg, consumerSettings.MessageType);
                exceptionResult = e;
            }
            return exceptionResult;
        }

        private void OnMessageExpired(DateTimeOffset? expires, object message, DateTimeOffset currentTime, TMessage nativeMessage)
        {
            logger.LogWarning("The message arrived too late and is already expired (expires {0}, current {1})", expires.Value, currentTime);

            try
            {
                // Execute the event hook
                consumerSettings.OnMessageExpired?.Invoke(messageBus, consumerSettings, message, nativeMessage);
                messageBus.Settings.OnMessageExpired?.Invoke(messageBus, consumerSettings, message, nativeMessage);
            }
            catch (Exception eh)
            {
                MessageBusBase.HookFailed(logger, eh, nameof(IConsumerEvents.OnMessageExpired));
            }
        }

        private string OnMessageError(object message, Exception e, TMessage nativeMessage)
        {
            string responseError = null;

            if (consumerSettings.ConsumerMode == ConsumerMode.RequestResponse)
            {
                logger.LogError(e, "Handler execution failed");
                // Save the exception
                responseError = e.ToString();
            }
            else
            {
                logger.LogError(e, "Consumer execution failed");
            }

            try
            {
                // Execute the event hook
                consumerSettings.OnMessageFault?.Invoke(messageBus, consumerSettings, message, e, nativeMessage);
                messageBus.Settings.OnMessageFault?.Invoke(messageBus, consumerSettings, message, e, nativeMessage);
            }
            catch (Exception eh)
            {
                MessageBusBase.HookFailed(logger, eh, nameof(IConsumerEvents.OnMessageFault));
            }

            return responseError;
        }

        private void OnMessageArrived(object message, TMessage nativeMessage)
        {
            try
            {
                // Execute the event hook
                consumerSettings.OnMessageArrived?.Invoke(messageBus, consumerSettings, message, consumerSettings.Path, nativeMessage);
                messageBus.Settings.OnMessageArrived?.Invoke(messageBus, consumerSettings, message, consumerSettings.Path, nativeMessage);
            }
            catch (Exception eh)
            {
                MessageBusBase.HookFailed(logger, eh, nameof(IConsumerEvents.OnMessageArrived));
            }
        }

        private void OnMessageFinished(object message, TMessage nativeMessage)
        {
            try
            {
                // Execute the event hook
                consumerSettings.OnMessageFinished?.Invoke(messageBus, consumerSettings, message, consumerSettings.Path, nativeMessage);
                messageBus.Settings.OnMessageFinished?.Invoke(messageBus, consumerSettings, message, consumerSettings.Path, nativeMessage);
            }
            catch (Exception eh)
            {
                MessageBusBase.HookFailed(logger, eh, nameof(IConsumerEvents.OnMessageFinished));
            }
        }

        private async Task<object> ExecuteConsumer(TMessage msg, object message, IDictionary<string, object> messageHeaders, object consumerInstance, IMessageTypeConsumerInvokerSettings consumerInvoker)
        {
            if (consumerWithContext)
            {
                var consumerContext = new ConsumerContext
                {
                    Headers = new ReadOnlyDictionary<string, object>(messageHeaders)
                };

                consumerContextInitializer?.Invoke(msg, consumerContext);

                var consumerWithContext = (IConsumerWithContext)consumerInstance;
                consumerWithContext.Context = consumerContext;
            }

            // the consumer just subscribes to the message
            var task = (consumerInvoker ?? consumerSettings).ConsumerMethod(consumerInstance, message, consumerSettings.Path);
            await task.ConfigureAwait(false);

            object response = null;

            if (consumerSettings.ConsumerMode == ConsumerMode.RequestResponse)
            {
                // the consumer handles the request (and replies)
                response = consumerSettings.ConsumerMethodResult(task);
            }

            return response;
        }

        protected object DeserializeMessage(TMessage msg, out IDictionary<string, object> headers, out string requestId, out DateTimeOffset? expires, IMessageTypeConsumerInvokerSettings invoker)
        {
            var messageWithHeaders = messageProvider(msg);

            headers = messageWithHeaders.Headers;

            logger.LogDebug("Deserializing message...");
            var messageType = invoker?.MessageType ?? consumerSettings.MessageType;
            var message = messageBus.Serializer.Deserialize(messageType, messageWithHeaders.Payload);

            requestId = null;
            expires = null;

            if (messageWithHeaders.Headers != null)
            {
                messageWithHeaders.Headers.TryGetHeader(ReqRespMessageHeaders.RequestId, out requestId);
                messageWithHeaders.Headers.TryGetHeader(ReqRespMessageHeaders.Expires, out expires);
            }

            return message;
        }

        private async Task ProduceResponse(string requestId, object request, IDictionary<string, object> requestHeaders, object response, string responseError)
        {
            // send the response (or error response)
            logger.LogDebug("Serializing the response {Response} of type {MessageType} for RequestId: {RequestId}...", response, consumerSettings.ResponseType, requestId);

            var responseHeaders = messageBus.CreateHeaders();
            responseHeaders.SetHeader(ReqRespMessageHeaders.RequestId, requestId);
            responseHeaders.SetHeader(ReqRespMessageHeaders.Error, responseError);

            await messageBus.ProduceResponse(request, requestHeaders, response, responseHeaders, consumerSettings).ConfigureAwait(false);
        }
    }
}