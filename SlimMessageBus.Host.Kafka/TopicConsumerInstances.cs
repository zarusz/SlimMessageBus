using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Common.Logging;
using Confluent.Kafka;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.Kafka
{
    public class TopicConsumerInstances : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger<TopicConsumerInstances>();

        private readonly List<object> _consumerInstances;
        private readonly BufferBlock<object> _consumerQueue;
        private readonly Queue<MessageProcessingResult> _messages = new Queue<MessageProcessingResult>();
        private readonly ConsumerSettings _settings;
        private readonly KafkaMessageBus _messageBus;
        private readonly KafkaGroupConsumer _groupConsumer;
        private readonly MethodInfo _consumerInstanceOnHandleMethod;
        private readonly PropertyInfo _taskResult;

        public TopicConsumerInstances(ConsumerSettings settings, KafkaGroupConsumer groupConsumer, KafkaMessageBus messageBus)
        {
            _settings = settings;
            _messageBus = messageBus;
            _groupConsumer = groupConsumer;

            _consumerInstanceOnHandleMethod = settings.ConsumerType.GetMethod("OnHandle", new[] { groupConsumer.MessageType, typeof(string) });
            _consumerInstances = ResolveInstances(settings, messageBus);
            _consumerQueue = new BufferBlock<object>();
            _consumerInstances.ForEach(x => _consumerQueue.Post(x));

            if (_settings.ConsumerMode == ConsumerMode.RequestResponse)
            {
                var taskType = typeof(Task<>).MakeGenericType(_settings.ResponseType);
                _taskResult = taskType.GetProperty("Result");
            }
        }

        private static List<object> ResolveInstances(ConsumerSettings settings, MessageBusBase messageBusBus)
        {
            var consumers = new List<object>();
            for (var i = 0; i < settings.Instances; i++)
            {
                var consumer = messageBusBus.Settings.DependencyResolver.Resolve(settings.ConsumerType);
                consumers.Add(consumer);
            }
            return consumers;
        }

        private int _commitBatchSize = 10;

        public void EnqueueMessage(Message msg)
        {
            _messages.Enqueue(new MessageProcessingResult(ProcessMessage(msg), msg));

            // ToDo: add timer trigger
            if (_messages.Count >= _commitBatchSize)
            {
                Log.DebugFormat("Reached {0} message threshold - will commit at offset {1}", _commitBatchSize, msg.TopicPartitionOffset);
                Commit(msg.TopicPartitionOffset);
            }
        }

        protected async Task ProcessMessage(Message msg)
        {
            string requestId = null, replyTo = null;
            DateTimeOffset? expires = null;
            var message = _settings.IsRequestMessage
                ? _messageBus.DeserializeRequest(_settings.MessageType, msg.Value, out requestId, out replyTo, out expires)
                : _messageBus.Settings.Serializer.Deserialize(_groupConsumer.MessageType, msg.Value);

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
                var task = (Task)_consumerInstanceOnHandleMethod.Invoke(obj, new[] { message, msg.Topic });
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

        #region IDisposable

        public void Dispose()
        {
            foreach (var consumerInstance in _consumerInstances.OfType<IDisposable>())
            {
                consumerInstance.DisposeSilently("consumer instance", Log);
            }
            _consumerInstances.Clear();
        }

        #endregion

        public void Commit(TopicPartitionOffset offset)
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

                    // ToDo
                    // some tasks failed

                    //_messages.OrderBy(x => x.Message.)

                }
            }
            _groupConsumer.Commit(offset).Wait();
            _messages.Clear();
        }
    }
}