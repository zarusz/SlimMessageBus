using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host
{
    /// <summary>
    /// Represents a pool of consumer instance that compete to handle a message.
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    public class ConsumerInstancePoolMessageProcessor<TMessage> : IMessageProcessor<TMessage> where TMessage : class
    {
        private readonly ILogger _logger;

        private readonly List<object> _instances;
        private readonly BufferBlock<object> _instancesQueue;

        private readonly MessageBusBase _messageBus;
        private readonly ConsumerSettings _consumerSettings;

        private readonly Func<TMessage, byte[]> _messagePayloadProvider;

        private readonly bool _consumerWithContext;
        private readonly Action<TMessage, ConsumerContext> _consumerContextInitializer;

        public ConsumerInstancePoolMessageProcessor(ConsumerSettings consumerSettings, MessageBusBase messageBus, Func<TMessage, byte[]> messagePayloadProvider, Action<TMessage, ConsumerContext> consumerContextInitializer = null)
        {
            if (messageBus is null) throw new ArgumentNullException(nameof(messageBus));

            _logger = messageBus.LoggerFactory.CreateLogger<ConsumerInstancePoolMessageProcessor<TMessage>>();
            _consumerSettings = consumerSettings ?? throw new ArgumentNullException(nameof(consumerSettings));
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _messagePayloadProvider = messagePayloadProvider ?? throw new ArgumentNullException(nameof(messagePayloadProvider));

            _consumerContextInitializer = consumerContextInitializer;
            _consumerWithContext = typeof(IConsumerContextAware).IsAssignableFrom(consumerSettings.ConsumerType);

            _instancesQueue = new BufferBlock<object>();
            _instances = ResolveInstances(consumerSettings);
            _instances.ForEach(x => _instancesQueue.Post(x));
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
                if (_instances.Count > 0)
                {
                    // dispose instances that implement IDisposable
                    foreach (var instance in _instances.OfType<IDisposable>())
                    {
                        instance.DisposeSilently("Consumer", _logger);
                    }
                    _instances.Clear();
                }
            }
        }

        #endregion

        /// <summary>
        /// Resolve N instances of consumer from the DI
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        private List<object> ResolveInstances(ConsumerSettings settings)
        {
            var consumers = new List<object>(settings.Instances);
            // Resolve as many instances from DI as requested in settings
            for (var i = 0; i < settings.Instances; i++)
            {
                _logger.LogDebug("Resolving Consumer instance {0} of type {1}", i + 1, settings.ConsumerType);
                try
                {
                    var consumer = _messageBus.Settings.DependencyResolver.Resolve(settings.ConsumerType);
                    consumers.Add(consumer);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while resolving consumer instance of type {0} from the dependency resolver", settings.ConsumerType);
                    throw;
                }
            }
            return consumers;
        }

        public virtual async Task<Exception> ProcessMessage(TMessage msg)
        {
            Exception exceptionResult = null;
            try
            {
                DeserializeMessage(msg, out var requestMessage, out var requestId, out var expires, out var message);

                // Verify if the request/message is already expired
                if (expires != null)
                {
                    var currentTime = _messageBus.CurrentTime;
                    if (currentTime > expires.Value)
                    {
                        _logger.LogWarning("The message arrived too late and is already expired (expires {0}, current {1})", expires.Value, currentTime);

                        try
                        {
                            // Execute the event hook
                            _consumerSettings.OnMessageExpired?.Invoke(_messageBus, _consumerSettings, message);
                            _messageBus.Settings.OnMessageExpired?.Invoke(_messageBus, _consumerSettings, message);
                        }
                        catch (Exception eh)
                        {
                            MessageBusBase.HookFailed(_logger, eh, nameof(IConsumerEvents.OnMessageExpired));
                        }

                        // Do not process the expired message
                        return null;
                    }
                }

                object response = null;
                string responseError = null;

                var consumerInstance = await _instancesQueue.ReceiveAsync(_messageBus.CancellationToken).ConfigureAwait(false);
                try
                {
                    if (_consumerWithContext && _consumerContextInitializer != null)
                    {
                        var consumerContext = new ConsumerContext();
                        _consumerContextInitializer(msg, consumerContext);

                        var consumerWithContext = (IConsumerContextAware)consumerInstance;
                        consumerWithContext.Context.Value = consumerContext;
                    }

                    try
                    {
                        // Execute the event hook
                        _consumerSettings.OnMessageArrived?.Invoke(_messageBus, _consumerSettings, message, _consumerSettings.Topic);
                        _messageBus.Settings.OnMessageArrived?.Invoke(_messageBus, _consumerSettings, message, _consumerSettings.Topic);
                    }
                    catch (Exception eh)
                    {
                        MessageBusBase.HookFailed(_logger, eh, nameof(IConsumerEvents.OnMessageArrived));
                    }

                    // the consumer just subscribes to the message
                    var task = _consumerSettings.ConsumerMethod(consumerInstance, message, _consumerSettings.Topic);
                    await task.ConfigureAwait(false);

                    if (_consumerSettings.ConsumerMode == ConsumerMode.RequestResponse)
                    {
                        // the consumer handles the request (and replies)
                        response = _consumerSettings.ConsumerMethodResult(task);
                    }
                }
                catch (Exception e)
                {
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
                        _consumerSettings.OnMessageFault?.Invoke(_messageBus, _consumerSettings, message, e);
                        _messageBus.Settings.OnMessageFault?.Invoke(_messageBus, _consumerSettings, message, e);
                    }
                    catch (Exception eh)
                    {
                        MessageBusBase.HookFailed(_logger, eh, nameof(IConsumerEvents.OnMessageFault));
                    }

                    exceptionResult = e;
                }
                finally
                {
                    await _instancesQueue.SendAsync(consumerInstance).ConfigureAwait(false);
                }

                if (response != null || responseError != null)
                {
                    // send the response (or error response)
                    _logger.LogDebug("Serializing the response {0} of type {1} for RequestId: {2}...", response, _consumerSettings.ResponseType, requestId);

                    var responseMessage = new MessageWithHeaders();
                    responseMessage.SetHeader(ReqRespMessageHeaders.RequestId, requestId);
                    responseMessage.SetHeader(ReqRespMessageHeaders.Error, responseError);

                    await _messageBus.ProduceResponse(message, requestMessage, response, responseMessage, _consumerSettings).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Processing of the message {0} of type {1} failed", msg, _consumerSettings.MessageType);
                exceptionResult = e;

            }
            return exceptionResult;
        }

        protected void DeserializeMessage(TMessage msg, out MessageWithHeaders requestMessage, out string requestId, out DateTimeOffset? expires, out object message)
        {
            var msgPayload = _messagePayloadProvider(msg);

            requestMessage = null;
            requestId = null;
            expires = null;

            _logger.LogDebug("Deserializing message...");

            message = _consumerSettings.IsRequestMessage
                ? _messageBus.DeserializeRequest(_consumerSettings.MessageType, msgPayload, out requestMessage)
                : _messageBus.Settings.Serializer.Deserialize(_consumerSettings.MessageType, msgPayload);

            if (requestMessage != null)
            {
                requestMessage.TryGetHeader(ReqRespMessageHeaders.RequestId, out requestId);
                requestMessage.TryGetHeader(ReqRespMessageHeaders.Expires, out expires);
            }
        }
    }
}