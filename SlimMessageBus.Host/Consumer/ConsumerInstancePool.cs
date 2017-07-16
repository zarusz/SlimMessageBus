using System;
using System.Collections.Generic;
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
        private static readonly ILog Log = LogManager.GetLogger(typeof(ConsumerInstancePool<TMessage>));

        private readonly List<object> _instances;
        private readonly BufferBlock<object> _instancesQueue;

        private readonly MessageBusBase _messageBus;
        private readonly ConsumerSettings _consumerSettings;

        private readonly Func<TMessage, byte[]> _messagePayloadProvider;

        private readonly MethodInfo _consumerOnHandleMethod;
        private readonly PropertyInfo _taskResultProperty;

        public ConsumerInstancePool(ConsumerSettings consumerSettings, MessageBusBase messageBus, Func<TMessage, byte[]> messagePayloadProvider)
        {
            _consumerSettings = consumerSettings;
            _messageBus = messageBus;
            _messagePayloadProvider = messagePayloadProvider;

            _consumerOnHandleMethod = consumerSettings.ConsumerType.GetMethod(nameof(IConsumer<object>.OnHandle), new[] { consumerSettings.MessageType, typeof(string) });

            _instancesQueue = new BufferBlock<object>();
            _instances = ResolveInstances(consumerSettings, messageBus);
            _instances.ForEach(x => _instancesQueue.Post(x));

            if (_consumerSettings.ConsumerMode == ConsumerMode.RequestResponse)
            {
                var taskType = typeof(Task<>).MakeGenericType(_consumerSettings.ResponseType);
                _taskResultProperty = taskType.GetProperty(nameof(Task<object>.Result));
            }
        }

        #region IDisposable

        public void Dispose()
        {
            if (_instances.Any())
            {
                // dospose instances that implement IDisposable
                foreach (var instance in _instances.OfType<IDisposable>())
                {
                    instance.DisposeSilently("Consumer", Log);
                }
                _instances.Clear();
            }
        }

        #endregion

        private static List<object> ResolveInstances(ConsumerSettings settings, MessageBusBase messageBus)
        {
            var consumers = new List<object>();
            // Resolve as many instances from DI as requested in settings
            for (var i = 0; i < settings.Instances; i++)
            {
                Log.DebugFormat("Resolving Consumer instance {0} of type {1}", i + 1, settings.ConsumerType);
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

            Log.Debug("Deserializing message...");
            var message = _consumerSettings.IsRequestMessage
                ? _messageBus.DeserializeRequest(_consumerSettings.MessageType, msgPayload, out requestId, out replyTo, out expires)
                : _messageBus.Settings.Serializer.Deserialize(_consumerSettings.MessageType, msgPayload);

            // Verify if the request/message is already expired
            if (expires.HasValue)
            {
                var currentTime = _messageBus.CurrentTime;
                if (currentTime > expires.Value)
                {
                    Log.DebugFormat("The message arrived too late and is already expired (expires {0}, current {1})", expires.Value, currentTime);
                    
                    // Execute the event hook
                    (_consumerSettings.OnMessageExpired ?? _messageBus.Settings.OnMessageExpired)?.Invoke(_consumerSettings, message);

                    // Do not process the expired message
                    return;
                }
            }

            object response = null;
            string responseError = null;

            var consumerInstance = await _instancesQueue.ReceiveAsync(_messageBus.CancellationToken);
            try
            {
                // the consumer just subscribes to the message
                var task = (Task)_consumerOnHandleMethod.Invoke(consumerInstance, new[] { message, _consumerSettings.Topic });
                await task;

                if (_consumerSettings.ConsumerMode == ConsumerMode.RequestResponse)
                {
                    // the consumer handles the request (and replies)
                    response = _taskResultProperty.GetValue(task);
                }
            }
            catch (Exception e)
            {
                if (_consumerSettings.ConsumerMode == ConsumerMode.RequestResponse)
                {
                    Log.DebugFormat("Handler execution failed", e);
                    // Save the exception
                    responseError = e.ToString();
                }
                else
                {
                    Log.ErrorFormat("Consumer execution failed", e);
                }

                try
                {
                    // Execute the event hook
                    (_consumerSettings.OnMessageFault ?? _messageBus.Settings.OnMessageFault)?.Invoke(_consumerSettings, message, e);
                }
                catch (Exception eh)
                {
                    // When the hook itself error out, catch the exception
                    Log.ErrorFormat("{0} method failed", eh, nameof(IConsumerEvents.OnMessageFault));
                }
            }
            finally
            {
                await _instancesQueue.SendAsync(consumerInstance);
            }

            if (response != null || responseError != null)
            {
                // send the response (or error response)
                Log.Debug("Serializing the respoonse...");
                var responsePayload = _messageBus.SerializeResponse(_consumerSettings.ResponseType, response, requestId, responseError);
                await _messageBus.Publish(_consumerSettings.ResponseType, responsePayload, replyTo);
            }
        }
    }
}