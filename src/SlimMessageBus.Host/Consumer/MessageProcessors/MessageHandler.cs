namespace SlimMessageBus.Host
{
    using Microsoft.Extensions.Logging;
    using SlimMessageBus.Host.Collections;
    using SlimMessageBus.Host.Config;
    using SlimMessageBus.Host.Interceptor;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public class MessageHandler
    {
        private readonly ILogger logger;
        protected readonly MessageBusBase messageBus;
        protected readonly ConsumerSettings consumerSettings;
        protected readonly RuntimeTypeCache runtimeTypeCache;

        private readonly bool consumerWithContext;
        private readonly Action<object, ConsumerContext> consumerContextInitializer;

        public ConsumerSettings ConsumerSettings => consumerSettings;
        public MessageBusBase MessageBus => messageBus;

        public MessageHandler(ConsumerSettings consumerSettings, MessageBusBase messageBus, Action<object, ConsumerContext> consumerContextInitializer = null)
        {
            if (messageBus is null) throw new ArgumentNullException(nameof(messageBus));

            logger = messageBus.LoggerFactory.CreateLogger<MessageHandler>();
            this.consumerSettings = consumerSettings ?? throw new ArgumentNullException(nameof(consumerSettings));
            this.messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            this.consumerContextInitializer = consumerContextInitializer;

            consumerWithContext = typeof(IConsumerWithContext).IsAssignableFrom(consumerSettings.ConsumerType);
            runtimeTypeCache = messageBus.RuntimeTypeCache;
        }

        public async Task<(object Response, Exception ResponseException, string RequestId)> DoHandle(object message, IDictionary<string, object> messageHeaders, object nativeMessage = null, IMessageTypeConsumerInvokerSettings consumerInvoker = null)
        {
            var messageType = message.GetType();

            var hasResponse = consumerSettings.ConsumerMode == ConsumerMode.RequestResponse;
            var responseType = hasResponse ? consumerSettings.ResponseType : null;

            object response = null;
            Exception responseException = null;
            string requestId = null;

            if (hasResponse && (messageHeaders == null || !messageHeaders.TryGetHeader(ReqRespMessageHeaders.RequestId, out requestId)))
            {
                throw new MessageBusException($"The message header {ReqRespMessageHeaders.RequestId} was not present at this time");
            }

            using (var messageScope = messageBus.GetMessageScope(consumerSettings, message))
            {
                if (messageHeaders != null && messageHeaders.TryGetHeader(ReqRespMessageHeaders.Expires, out DateTimeOffset? expires) && expires != null)
                {
                    // Verify if the request/message is already expired
                    var currentTime = messageBus.CurrentTime;
                    if (currentTime > expires.Value)
                    {
                        // ToDo: Call interceptor
                        OnMessageExpired(expires, message, currentTime, nativeMessage);

                        // Do not process the expired message
                        return (null, null, requestId);
                    }
                }

                OnMessageArrived(message, nativeMessage);

                // ToDo: Introduce CTs
                var ct = new CancellationToken();

                var consumerType = consumerInvoker?.ConsumerType ?? consumerSettings.ConsumerType;
                var consumerInstance = messageScope.Resolve(consumerType)
                    ?? throw new ConfigurationMessageBusException($"Could not resolve consumer/handler type {consumerType} from the DI container. Please check that the configured type {consumerType} is registered within the DI container.");
                try
                {
                    var consumerInterceptors = runtimeTypeCache.ConsumerInterceptorType.ResolveAll(messageScope, messageType);
                    var handlerInterceptors = hasResponse ? runtimeTypeCache.HandlerInterceptorType.ResolveAll(messageScope, messageType, responseType) : null;
                    if (consumerInterceptors != null || handlerInterceptors != null)
                    {
                        var next = () => ExecuteConsumer(nativeMessage, message, messageHeaders, consumerInstance, consumerInvoker);

                        // call with interceptors
                        if (consumerInterceptors != null)
                        {
                            var consumerInterceptorType = runtimeTypeCache.ConsumerInterceptorType.Get(messageType);
                            foreach (var consumerInterceptor in consumerInterceptors.OfType<IInterceptor>().OrderBy(x => x.GetOrder()))
                            {
                                var interceptorParams = new object[] { message, ct, next, messageBus, consumerSettings.Path, messageHeaders, consumerInstance };
                                next = () => (Task<object>)consumerInterceptorType.Method.Invoke(consumerInterceptor, interceptorParams);
                            }
                        }

                        if (handlerInterceptors != null)
                        {
                            var handlerInterceptorType = runtimeTypeCache.HandlerInterceptorType.Get(messageType, responseType);
                            foreach (var handlerInterceptor in handlerInterceptors.OfType<IInterceptor>().OrderBy(x => x.GetOrder()))
                            {
                                var interceptorParams = new object[] { message, ct, next, messageBus, consumerSettings.Path, messageHeaders, consumerInstance };
                                next = () => (Task<object>)handlerInterceptorType.Method.Invoke(handlerInterceptor, interceptorParams);
                            }
                        }

                        response = await next();
                    }
                    else
                    {
                        // call without interceptors
                        response = await ExecuteConsumer(nativeMessage, message, messageHeaders, consumerInstance, consumerInvoker).ConfigureAwait(false);
                    }
                }
                catch (Exception e)
                {
                    OnMessageError(message, e, nativeMessage);
                    responseException = e;
                }
                finally
                {
                    if (consumerSettings.IsDisposeConsumerEnabled && consumerInstance is IDisposable consumerInstanceDisposable)
                    {
                        logger.LogDebug("Disposing consumer instance {Consumer} of type {ConsumerType}", consumerInstance, consumerType);
                        consumerInstanceDisposable.DisposeSilently("ConsumerInstance", logger);
                    }
                }

                OnMessageFinished(message, nativeMessage);
            }

            return (response, responseException, requestId);
        }

        private async Task<object> ExecuteConsumer(object nativeMessage, object message, IDictionary<string, object> messageHeaders, object consumerInstance, IMessageTypeConsumerInvokerSettings consumerInvoker)
        {
            if (consumerWithContext)
            {
                var consumerContext = new ConsumerContext
                {
                    Headers = new ReadOnlyDictionary<string, object>(messageHeaders)
                };

                consumerContextInitializer?.Invoke(nativeMessage, consumerContext);

                var consumerWithContext = (IConsumerWithContext)consumerInstance;
                consumerWithContext.Context = consumerContext;
            }

            // the consumer just subscribes to the message
            var task = (consumerInvoker ?? consumerSettings).ConsumerMethod(consumerInstance, message, consumerSettings.Path);
            await task.ConfigureAwait(false);

            if (consumerSettings.ConsumerMode == ConsumerMode.RequestResponse)
            {
                // the consumer handles the request (and replies)
                var response = consumerSettings.ConsumerMethodResult(task);
                return response;
            }

            return null;
        }

        private void OnMessageExpired(DateTimeOffset? expires, object message, DateTimeOffset currentTime, object nativeMessage)
        {
            logger.LogWarning("The message {Message} arrived too late and is already expired (expires {ExpiresAt}, current {Time})", message, expires.Value, currentTime);

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

        private void OnMessageError(object message, Exception e, object nativeMessage)
        {
            logger.LogError(e, consumerSettings.ConsumerMode == ConsumerMode.RequestResponse ? "Handler execution failed" : "Consumer execution failed");

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
        }

        private void OnMessageArrived(object message, object nativeMessage)
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

        private void OnMessageFinished(object message, object nativeMessage)
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
    }
}