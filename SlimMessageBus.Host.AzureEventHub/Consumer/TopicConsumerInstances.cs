using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Common.Logging;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureEventHub
{
    public class TopicConsumerInstances<TMessage> : IDisposable
        where TMessage : class
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(TopicConsumerInstances<>));

        private readonly List<object> _consumerInstances;
        private readonly BufferBlock<object> _consumerQueue;
        private readonly Queue<MessageProcessingResult<TMessage>> _messages = new Queue<MessageProcessingResult<TMessage>>();
        private readonly ConsumerSettings _settings;
        private readonly EventHubMessageBus _messageBus;
        private readonly Func<TMessage, byte[]> _messagePayloadProvider;
        private readonly EventHubConsumer _consumer;

        private readonly MethodInfo _consumerInstanceOnHandleMethod;
        private readonly PropertyInfo _taskResult;

        public TopicConsumerInstances(ConsumerSettings settings, EventHubConsumer consumer, EventHubMessageBus messageBus, Func<TMessage, byte[]> messagePayloadProvider)
        {
            _settings = settings;
            _messageBus = messageBus;
            _consumer = consumer;
            _messagePayloadProvider = messagePayloadProvider;

            _consumerInstanceOnHandleMethod = settings.ConsumerType.GetMethod(nameof(IConsumer<object>.OnHandle), new[] { consumer.MessageType, typeof(string) });
            _consumerInstances = ResolveInstances(settings, messageBus);
            _consumerQueue = new BufferBlock<object>();
            _consumerInstances.ForEach(x => _consumerQueue.Post(x));

            if (_settings.ConsumerMode == ConsumerMode.RequestResponse)
            {
                var taskType = typeof(Task<>).MakeGenericType(_settings.ResponseType);
                _taskResult = taskType.GetProperty(nameof(Task<object>.Result));
            }
        }

        #region IDisposable

        public void Dispose()
        {
            if (_consumerInstances.Any())
            {
                foreach (var consumerInstance in _consumerInstances.OfType<IDisposable>())
                {
                    consumerInstance.DisposeSilently("consumer instance", Log);
                }
                _consumerInstances.Clear();
            }
        }

        #endregion

        private static List<object> ResolveInstances(ConsumerSettings settings, MessageBusBase messageBus)
        {
            var consumers = new List<object>();
            // Resolve as many instances from DI as requested in settings
            for (var i = 0; i < settings.Instances; i++)
            {
                var consumer = messageBus.Settings.DependencyResolver.Resolve(settings.ConsumerType);
                consumers.Add(consumer);
            }
            return consumers;
        }

        private int _commitBatchSize = 10;

        public TMessage Submit(TMessage message)
        {
            var messageTask = ProcessMessage(message);
            _messages.Enqueue(new MessageProcessingResult<TMessage>(messageTask, message));

            // ToDo: add timer trigger
            if (_messages.Count >= _commitBatchSize)
            {
                return Commit(message);
            }
            return null;
        }

        public TMessage Commit(TMessage lastMessage)
        {
            if (_messages.Count > 0)
            {
                try
                {
                    var tasks = _messages.Select(x => x.Task).ToArray();
                    Task.WaitAll(tasks);
                }
                catch (AggregateException e)
                {
                    Log.ErrorFormat("Errors occured while executing the tasks {0}", e);
                    // ToDo: some tasks failed
                }
                _messages.Clear();
            }
            return lastMessage;
        }

        protected async Task ProcessMessage(TMessage msg)
        {
            var msgPayload = _messagePayloadProvider(msg);

            string requestId = null, replyTo = null;
            DateTimeOffset? expires = null;
            var message = _settings.IsRequestMessage
                ? _messageBus.DeserializeRequest(_settings.MessageType, msgPayload, out requestId, out replyTo, out expires)
                : _messageBus.Settings.Serializer.Deserialize(_consumer.MessageType, msgPayload);

            // Verify if the request/message is already expired
            if (expires.HasValue)
            {
                var currentTime = _messageBus.CurrentTime;
                if (currentTime > expires.Value)
                {
                    Log.DebugFormat("The request message arrived late and is already expired (expires {0}, current {1})", expires.Value, currentTime);
                    // Do not process the expired message

                    // ToDo: add and API hook to these kind of situation
                    return;
                }
            }

            object response = null;
            string responseError = null;

            var obj = await _consumerQueue.ReceiveAsync(_messageBus.CancellationToken);
            try
            {
                // the consumer just subscribes to the message
                var task = (Task)_consumerInstanceOnHandleMethod.Invoke(obj, new[] { message, _consumer.Topic });
                try
                {
                    await task;

                    if (_settings.ConsumerMode == ConsumerMode.RequestResponse)
                    {
                        // the consumer handles the request (and replies)
                        response = _taskResult.GetValue(task);
                    }
                }
                catch (Exception e)
                {
                    if (_settings.ConsumerMode == ConsumerMode.RequestResponse)
                    {
                        Log.DebugFormat("Handler execution failed", e);
                        responseError = e.ToString();
                    }
                    else
                    {
                        Log.ErrorFormat("Consumer execution failed", e);
                    }
                }

            }
            finally
            {
                await _consumerQueue.SendAsync(obj);
            }

            if (response != null || responseError != null)
            {
                // send the response (or error response)
                var responsePayload = _messageBus.SerializeResponse(_settings.ResponseType, response, requestId, responseError);
                await _messageBus.Publish(_settings.ResponseType, responsePayload, replyTo);
            }
        }
    }
}