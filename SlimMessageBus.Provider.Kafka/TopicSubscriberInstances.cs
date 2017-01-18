using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Common.Logging;
using RdKafka;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Provider.Kafka
{
    public class TopicConsumerInstances : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger<TopicConsumerInstances>();

        private readonly List<object> _consumerInstances;
        private readonly BufferBlock<object> _consumerQueue;
        private readonly Queue<MessageProcessingResult> _messages = new Queue<MessageProcessingResult>();
        private readonly SubscriberSettings _settings;
        private readonly KafkaMessageBus _messageBus;
        private readonly KafkaGroupConsumer _groupConsumer;
        private readonly MethodInfo _consumerInstanceOnHandleMethod;
        private readonly PropertyInfo _taskResult;

        public TopicConsumerInstances(SubscriberSettings settings, KafkaGroupConsumer groupConsumer, KafkaMessageBus messageBus)
        {
            _settings = settings;
            _messageBus = messageBus;
            _groupConsumer = groupConsumer;

            _consumerInstanceOnHandleMethod = settings.ConsumerType.GetMethod("OnHandle", new[] { groupConsumer.MessageType, typeof(string) });
            _consumerInstances = ResolveInstances(settings, messageBus);
            _consumerQueue = new BufferBlock<object>();
            _consumerInstances.ForEach(x => _consumerQueue.Post(x));

            var taskType = typeof(Task<>).MakeGenericType(_settings.ResponseType);
            _taskResult = taskType.GetProperty("Result");

        }

        private static List<object> ResolveInstances(SubscriberSettings settings, MessageBusBase messageBusBus)
        {
            var subscribers = new List<object>();
            for (var i = 0; i < settings.Instances; i++)
            {
                var subscriber = messageBusBus.Settings.DependencyResolver.Resolve(settings.ConsumerType).ToList();

                Assert.IsFalse(subscriber.Count == 0,
                    () => new ConfigurationMessageBusException($"There was no implementation of {settings.ConsumerType} returned by the resolver. Ensure you have registered an implementation for {settings.ConsumerType} in your DI container."));

                Assert.IsFalse(subscriber.Count > 1,
                    () => new ConfigurationMessageBusException($"More than one implementation of {settings.ConsumerType} returned by the resolver. Ensure you have registered exactly one implementation for {settings.ConsumerType} in your DI container."));

                subscribers.Add(subscriber[0]);
            }
            return subscribers;
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
                ? _messageBus.DeserializeRequest(_settings.MessageType, msg.Payload, out requestId, out replyTo, out expires)
                : _messageBus.Settings.Serializer.Deserialize(_groupConsumer.MessageType, msg.Payload);

            // Verify if the request/message is already expired
            if (expires.HasValue)
            {
                var currentTime = _messageBus.CurrentTime;
                if (currentTime > expires.Value)
                {
                    Log.DebugFormat("The request message arrived late and is already expired (expires {0}, current {1})", expires.Value, currentTime);
                    // Do not process the expired message
                    return;
                }
            }

            object response = null;

            var obj = await _consumerQueue.ReceiveAsync(_messageBus.CancellationToken);
            try
            {
                //var subscriber = (ISubscriber<object>)obj;
                //await subscriber.OnHandle(message, msg.Topic);

                // the consumer just subscribes to the message
                var task = (Task)_consumerInstanceOnHandleMethod.Invoke(obj, new[] { message, msg.Topic });
                await task;

                if (_settings.ConsumerMode == ConsumerMode.RequestResponse)
                {
                    // the consumer handles the request (and replies)
                    response = _taskResult.GetValue(task);
                }
            }
            finally
            {
                await _consumerQueue.SendAsync(obj);
            }

            if (response != null)
            {
                var responsePayload = _messageBus.SerializeResponse(_settings.ResponseType, response, requestId);
                await _messageBus.Publish(_settings.ResponseType, responsePayload, replyTo);
            }
        }

        #region IDisposable

        public void Dispose()
        {
            foreach (var consumerInstance in _consumerInstances.OfType<IDisposable>())
            {
                consumerInstance.DisposeSilently(e => Log.WarnFormat("Error occured while disposing consumer instance. {0}", e));
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