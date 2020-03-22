using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Common.Logging;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host
{
    /// <summary>
    /// Represents a pool of consumer instance that compete to handle a message.
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    public class ConsumerInstancePoolMessageProcessor<TMessage> : IMessageProcessor<TMessage>
        where TMessage : class
    {
        private readonly ILog _log = LogManager.GetLogger(typeof(ConsumerInstancePoolMessageProcessor<TMessage>).ToString()); // this will give a more friendly name without the assembly version of the generic param

        private readonly List<object> _instances;
        private readonly BufferBlock<object> _instancesQueue;

        private readonly MessageBusBase _messageBus;
        private readonly ConsumerSettings _consumerSettings;

        private readonly Func<TMessage, byte[]> _messagePayloadProvider;

        private readonly bool _consumerWithContext;
        private readonly Action<TMessage, ConsumerContext> _consumerContextInitializer;

        public ConsumerInstancePoolMessageProcessor(ConsumerSettings consumerSettings, MessageBusBase messageBus, Func<TMessage, byte[]> messagePayloadProvider, Action<TMessage, ConsumerContext> consumerContextInitializer = null)
        {
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
                        instance.DisposeSilently("Consumer", _log);
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
                _log.DebugFormat(CultureInfo.InvariantCulture, "Resolving Consumer instance {0} of type {1}", i + 1, settings.ConsumerType);
                try
                {
                    var consumer = _messageBus.Settings.DependencyResolver.Resolve(settings.ConsumerType);
                    consumers.Add(consumer);
                }
                catch (Exception ex)
                {
                    _log.ErrorFormat(CultureInfo.InvariantCulture, "Error while resolving consumer instance of type {0} from the dependency resolver", ex, settings.ConsumerType);
                    throw;
                }
            }
            return consumers;
        }

        public virtual async Task ProcessMessage(TMessage msg)
        {
            var msgPayload = _messagePayloadProvider(msg);

            MessageWithHeaders requestMessage = null;
            string requestId = null;
            DateTimeOffset? expires = null;

            _log.Debug("Deserializing message...");
            var message = _consumerSettings.IsRequestMessage
                ? _messageBus.DeserializeRequest(_consumerSettings.MessageType, msgPayload, out requestMessage)
                : _messageBus.Settings.Serializer.Deserialize(_consumerSettings.MessageType, msgPayload);

            if (requestMessage != null)
            {
                requestMessage.TryGetHeader(ReqRespMessageHeaders.RequestId, out requestId);
                requestMessage.TryGetHeader(ReqRespMessageHeaders.Expires, out expires);
            }

            // Verify if the request/message is already expired
            if (expires.HasValue)
            {
                var currentTime = _messageBus.CurrentTime;
                if (currentTime > expires.Value)
                {
                    _log.WarnFormat(CultureInfo.InvariantCulture, "The message arrived too late and is already expired (expires {0}, current {1})", expires.Value, currentTime);

                    try
                    {
                        // Execute the event hook
                        (_consumerSettings.OnMessageExpired ?? _messageBus.Settings.OnMessageExpired)?.Invoke(_messageBus, _consumerSettings, message);
                    }
                    catch (Exception eh)
                    {
                        MessageBusBase.HookFailed(_log, eh, nameof(IConsumerEvents.OnMessageExpired));
                    }

                    // Do not process the expired message
                    return;
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
                    _log.ErrorFormat(CultureInfo.InvariantCulture, "Handler execution failed", e);
                    // Save the exception
                    responseError = e.ToString();
                }
                else
                {
                    _log.ErrorFormat(CultureInfo.InvariantCulture, "Consumer execution failed", e);
                }

                try
                {
                    // Execute the event hook
                    (_consumerSettings.OnMessageFault ?? _messageBus.Settings.OnMessageFault)?.Invoke(_messageBus, _consumerSettings, message, e);
                }
                catch (Exception eh)
                {
                    MessageBusBase.HookFailed(_log, eh, nameof(IConsumerEvents.OnMessageFault));
                }
            }
            finally
            {
                await _instancesQueue.SendAsync(consumerInstance).ConfigureAwait(false);
            }

            if (response != null || responseError != null)
            {
                // send the response (or error response)
                _log.DebugFormat(CultureInfo.InvariantCulture, "Serializing the response {0} of type {1} for RequestId: {2}...", response, _consumerSettings.ResponseType, requestId);

                var responseMessage = new MessageWithHeaders();
                responseMessage.SetHeader(ReqRespMessageHeaders.RequestId, requestId);
                responseMessage.SetHeader(ReqRespMessageHeaders.Error, responseError);

                await _messageBus.ProduceResponse(message, requestMessage, response, responseMessage, _consumerSettings).ConfigureAwait(false);
            }
        }
    }
}