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
    public class ConsumerInstancePool<TMessage> : IDisposable
        where TMessage : class
    {
        private readonly ILog _log = LogManager.GetLogger(typeof(ConsumerInstancePool<TMessage>));

        private readonly List<object> _instances;
        private readonly BufferBlock<object> _instancesQueue;

        private readonly MessageBusBase _messageBus;
        private readonly ConsumerSettings _consumerSettings;
        private readonly ConsumerRuntimeInfo _consumerRuntimeInfo;

        private readonly Func<TMessage, byte[]> _messagePayloadProvider;

        private readonly bool _consumerWithContext;
        private readonly Action<TMessage, ConsumerContext> _consumerContextInitializer;

        public ConsumerInstancePool(ConsumerSettings consumerSettings, MessageBusBase messageBus, Func<TMessage, byte[]> messagePayloadProvider, Action<TMessage, ConsumerContext> consumerContextInitializer = null)
        {
            _consumerSettings = consumerSettings;
            _consumerRuntimeInfo = new ConsumerRuntimeInfo(consumerSettings);

            _messageBus = messageBus;
            _messagePayloadProvider = messagePayloadProvider;

            _consumerContextInitializer = consumerContextInitializer;
            _consumerWithContext = typeof(IConsumerContextAware).IsAssignableFrom(consumerSettings.ConsumerType);

            _instancesQueue = new BufferBlock<object>();
            _instances = ResolveInstances(consumerSettings, messageBus);
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
            if (!disposing)
            {
                return;
            }

            if (_instances.Any())
            {
                // dospose instances that implement IDisposable
                foreach (var instance in _instances.OfType<IDisposable>())
                {
                    instance.DisposeSilently("Consumer", _log);
                }

                _instances.Clear();
            }

        }

        #endregion

        private List<object> ResolveInstances(ConsumerSettings settings, MessageBusBase messageBus)
        {
            var consumers = new List<object>();
            // Resolve as many instances from DI as requested in settings
            for (var i = 0; i < settings.Instances; i++)
            {
                _log.DebugFormat(CultureInfo.InvariantCulture, "Resolving Consumer instance {0} of type {1}", i + 1, settings.ConsumerType);
                var consumer = messageBus.Settings.DependencyResolver.Resolve(settings.ConsumerType);
                consumers.Add(consumer);
            }
            return consumers;
        }

        public virtual async Task ProcessMessage(TMessage msg)
        {
            var msgPayload = _messagePayloadProvider(msg);

            string requestId = null, replyTo = null;
            DateTimeOffset? expires = null;

            _log.Debug("Deserializing message...");
            var message = _consumerSettings.IsRequestMessage
                ? _messageBus.DeserializeRequest(_consumerSettings.MessageType, msgPayload, out requestId, out replyTo, out expires)
                : _messageBus.Settings.Serializer.Deserialize(_consumerSettings.MessageType, msgPayload);

            // Verify if the request/message is already expired
            if (expires.HasValue)
            {
                var currentTime = _messageBus.CurrentTime;
                if (currentTime > expires.Value)
                {
                    _log.DebugFormat(CultureInfo.InvariantCulture, "The message arrived too late and is already expired (expires {0}, current {1})", expires.Value, currentTime);

                    // Execute the event hook
                    (_consumerSettings.OnMessageExpired ?? _messageBus.Settings.OnMessageExpired)?.Invoke(_consumerSettings, message);

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
                var task = _consumerRuntimeInfo.OnHandle(consumerInstance, message);
                await task.ConfigureAwait(false);

                if (_consumerSettings.ConsumerMode == ConsumerMode.RequestResponse)
                {
                    // the consumer handles the request (and replies)
                    response = _consumerRuntimeInfo.GetResponseValue(task);
                }
            }
            catch (Exception e)
            {
                if (_consumerSettings.ConsumerMode == ConsumerMode.RequestResponse)
                {
                    _log.DebugFormat(CultureInfo.InvariantCulture, "Handler execution failed", e);
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
                    (_consumerSettings.OnMessageFault ?? _messageBus.Settings.OnMessageFault)?.Invoke(_consumerSettings, message, e);
                }
                catch (Exception eh)
                {
                    // When the hook itself error out, catch the exception
                    _log.ErrorFormat(CultureInfo.InvariantCulture, "{0} method failed", eh, nameof(IConsumerEvents.OnMessageFault));
                }
            }
            finally
            {
                await _instancesQueue.SendAsync(consumerInstance).ConfigureAwait(false);
            }

            if (response != null || responseError != null)
            {
                // send the response (or error response)
                _log.Debug("Serializing the response...");
                var responsePayload = _messageBus.SerializeResponse(_consumerSettings.ResponseType, response, requestId, responseError);
                await _messageBus.PublishToTransport(_consumerSettings.ResponseType, response, replyTo, responsePayload).ConfigureAwait(false);
            }
        }
    }
}