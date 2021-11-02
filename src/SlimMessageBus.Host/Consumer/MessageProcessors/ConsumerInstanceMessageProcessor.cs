namespace SlimMessageBus.Host
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using SlimMessageBus.Host.Config;

    /// <summary>
    /// Implementation of <see cref="IMessageProcessor{TMessage}"/> that peforms orchestration around processing of a new message using an instance of the declared consumer (<see cref="IConsumer{TMessage}"/> or <see cref="IRequestHandler{TRequest, TResponse}"/> interface).
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    public class ConsumerInstanceMessageProcessor<TMessage> : IMessageProcessor<TMessage> where TMessage : class
    {
        private readonly ILogger _logger;

        private readonly MessageBusBase _messageBus;
        private readonly ConsumerSettings _consumerSettings;

        private readonly Func<TMessage, MessageWithHeaders> _messageProvider;

        private readonly bool _createMessageScope;

        private readonly bool _consumerWithContext;
        private readonly Action<TMessage, ConsumerContext> _consumerContextInitializer;

        private readonly bool _consumerWithHeaders;

        public ConsumerInstanceMessageProcessor(ConsumerSettings consumerSettings, MessageBusBase messageBus, Func<TMessage, MessageWithHeaders> messageProvider, Action<TMessage, ConsumerContext> consumerContextInitializer = null)
        {
            if (messageBus is null) throw new ArgumentNullException(nameof(messageBus));

            _logger = messageBus.LoggerFactory.CreateLogger<ConsumerInstancePoolMessageProcessor<TMessage>>();
            _consumerSettings = consumerSettings ?? throw new ArgumentNullException(nameof(consumerSettings));
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _messageProvider = messageProvider ?? throw new ArgumentNullException(nameof(messageProvider));

            _createMessageScope = _messageBus.IsMessageScopeEnabled(_consumerSettings);

            _consumerContextInitializer = consumerContextInitializer;
            _consumerWithContext = typeof(IConsumerContextAware).IsAssignableFrom(consumerSettings.ConsumerType);

            _consumerWithHeaders = typeof(IConsumerWithHeaders).IsAssignableFrom(consumerSettings.ConsumerType);
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

        public virtual async Task<Exception> ProcessMessage(TMessage msg)
        {
            Exception exceptionResult = null;
            try
            {
                var message = DeserializeMessage(msg, out var messageHeaders, out var requestId, out var expires);

                // Verify if the request/message is already expired
                if (expires != null)
                {
                    var currentTime = _messageBus.CurrentTime;
                    if (currentTime > expires.Value)
                    {
                        OnMessageExpired(expires, message, currentTime, msg);

                        // Do not process the expired message
                        return null;
                    }
                }

                object response = null;
                string responseError = null;

                if (_createMessageScope)
                {
                    _logger.LogDebug("Creating message scope for message {Message} of type {MessageType}", message, _consumerSettings.MessageType);
                }

                var messageScope = _createMessageScope
                    ? _messageBus.Settings.DependencyResolver.CreateScope()
                    : _messageBus.Settings.DependencyResolver;

                // Set MessageScope.Current, so any future integration might need to use that
                MessageScope.Current = messageScope;

                try
                {
                    OnMessageArrived(message, msg);

                    var consumerInstance = messageScope.Resolve(_consumerSettings.ConsumerType)
                        ?? throw new ConfigurationMessageBusException($"Could not resolve consumer/handler type {_consumerSettings.ConsumerType} from the DI container. Please check that the configure type {_consumerSettings.ConsumerType} is registered within the DI container.");
                    try
                    {
                        response = await ExecuteConsumer(msg, message, messageHeaders, consumerInstance).ConfigureAwait(false);
                    }
#pragma warning disable CA1031 // Do not catch general exception types - Intended
                    catch (Exception e)
#pragma warning restore CA1031 // Do not catch general exception types
                    {
                        responseError = OnMessageError(message, e, msg);
                        exceptionResult = e;
                    }
                    finally
                    {
                        OnMessageFinished(message, msg);

                        if (_consumerSettings.IsDisposeConsumerEnabled && consumerInstance is IDisposable consumerInstanceDisposable)
                        {
                            _logger.LogDebug("Disposing consumer instance {Consumer} of type {ConsumerType}", consumerInstance, _consumerSettings.ConsumerType);
                            consumerInstanceDisposable.DisposeSilently("ConsumerInstance", _logger);
                        }
                    }
                }
                finally
                {
                    // Clear the MessageScope.Current
                    MessageScope.Current = null;

                    if (_createMessageScope)
                    {
                        _logger.LogDebug("Disposing message scope for message {Message} of type {MessageType}", message, _consumerSettings.MessageType);
                        messageScope.DisposeSilently("Scope", _logger);
                    }
                }

                if (response != null || responseError != null)
                {
                    await ProduceResponse(requestId, message, messageHeaders, response, responseError).ConfigureAwait(false);
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types - intended to catch all exceptions
            catch (Exception e)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                _logger.LogError(e, "Processing of the message {Message} of type {ConsumerType} failed", msg, _consumerSettings.MessageType);
                exceptionResult = e;
            }
            return exceptionResult;
        }

        private void OnMessageExpired(DateTimeOffset? expires, object message, DateTimeOffset currentTime, TMessage nativeMessage)
        {
            _logger.LogWarning("The message arrived too late and is already expired (expires {0}, current {1})", expires.Value, currentTime);

            try
            {
                // Execute the event hook
                _consumerSettings.OnMessageExpired?.Invoke(_messageBus, _consumerSettings, message, nativeMessage);
                _messageBus.Settings.OnMessageExpired?.Invoke(_messageBus, _consumerSettings, message, nativeMessage);
            }
#pragma warning disable CA1031 // Intended, a catch all situation
            catch (Exception eh)
#pragma warning restore CA1031
            {
                MessageBusBase.HookFailed(_logger, eh, nameof(IConsumerEvents.OnMessageExpired));
            }
        }

        private string OnMessageError(object message, Exception e, TMessage nativeMessage)
        {
            string responseError = null;

            if (_consumerSettings.ConsumerMode == ConsumerMode.RequestResponse)
            {
                _logger.LogError(e, "Handler execution failed");
                // Save the exception
                responseError = e.ToString();
            }
            else
            {
                _logger.LogError(e, "Consumer execution failed");
            }

            try
            {
                // Execute the event hook
                _consumerSettings.OnMessageFault?.Invoke(_messageBus, _consumerSettings, message, e, nativeMessage);
                _messageBus.Settings.OnMessageFault?.Invoke(_messageBus, _consumerSettings, message, e, nativeMessage);
            }
#pragma warning disable CA1031 // Intended, a catch all situation
            catch (Exception eh)
#pragma warning restore CA1031
            {
                MessageBusBase.HookFailed(_logger, eh, nameof(IConsumerEvents.OnMessageFault));
            }

            return responseError;
        }

        private void OnMessageArrived(object message, TMessage nativeMessage)
        {
            try
            {
                // Execute the event hook
                _consumerSettings.OnMessageArrived?.Invoke(_messageBus, _consumerSettings, message, _consumerSettings.Path, nativeMessage);
                _messageBus.Settings.OnMessageArrived?.Invoke(_messageBus, _consumerSettings, message, _consumerSettings.Path, nativeMessage);
            }
#pragma warning disable CA1031 // Intended, a catch all situation
            catch (Exception eh)
#pragma warning restore CA1031
            {
                MessageBusBase.HookFailed(_logger, eh, nameof(IConsumerEvents.OnMessageArrived));
            }
        }

        private void OnMessageFinished(object message, TMessage nativeMessage)
        {
            try
            {
                // Execute the event hook
                _consumerSettings.OnMessageFinished?.Invoke(_messageBus, _consumerSettings, message, _consumerSettings.Path, nativeMessage);
                _messageBus.Settings.OnMessageFinished?.Invoke(_messageBus, _consumerSettings, message, _consumerSettings.Path, nativeMessage);
            }
#pragma warning disable CA1031 // Intended, a catch all situation
            catch (Exception eh)
#pragma warning restore CA1031
            {
                MessageBusBase.HookFailed(_logger, eh, nameof(IConsumerEvents.OnMessageFinished));
            }
        }

        private async Task<object> ExecuteConsumer(TMessage msg, object message, IDictionary<string, object> messageHeaders, object consumerInstance)
        {
            if (_consumerWithContext && _consumerContextInitializer != null)
            {
                var consumerContext = new ConsumerContext();
                _consumerContextInitializer(msg, consumerContext);

                var consumerWithContext = (IConsumerContextAware)consumerInstance;
                consumerWithContext.Context.Value = consumerContext;
            }

            if (_consumerWithHeaders)
            {
                var consumerWithHeaders = (IConsumerWithHeaders)consumerInstance;
                consumerWithHeaders.Headers = new ReadOnlyDictionary<string, object>(messageHeaders);
            }

            // the consumer just subscribes to the message
            var task = _consumerSettings.ConsumerMethod(consumerInstance, message, _consumerSettings.Path);
            await task.ConfigureAwait(false);

            object response = null;

            if (_consumerSettings.ConsumerMode == ConsumerMode.RequestResponse)
            {
                // the consumer handles the request (and replies)
                response = _consumerSettings.ConsumerMethodResult(task);
            }

            return response;
        }

        protected object DeserializeMessage(TMessage msg, out IDictionary<string, object> headers, out string requestId, out DateTimeOffset? expires)
        {
            var messageWithHeaders = _messageProvider(msg);

            headers = messageWithHeaders.Headers;

            _logger.LogDebug("Deserializing message...");
            var message = _messageBus.Serializer.Deserialize(_consumerSettings.MessageType, messageWithHeaders.Payload);

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
            _logger.LogDebug("Serializing the response {Response} of type {MessageType} for RequestId: {RequestId}...", response, _consumerSettings.ResponseType, requestId);

            var responseHeaders = _messageBus.CreateHeaders();
            responseHeaders.SetHeader(ReqRespMessageHeaders.RequestId, requestId);
            responseHeaders.SetHeader(ReqRespMessageHeaders.Error, responseError);

            await _messageBus.ProduceResponse(request, requestHeaders, response, responseHeaders, _consumerSettings).ConfigureAwait(false);
        }
    }
}