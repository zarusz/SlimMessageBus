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
    public class TopicSubscriberInstances : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger<TopicSubscriberInstances>();

        public string Topic { get; protected set; }

        private readonly List<object> _consumerInstances;
        private readonly BufferBlock<object> _consumerQueue;
        private readonly Queue<MessageProcessingResult> _messages = new Queue<MessageProcessingResult>(); 
        private readonly KafkaMessageBus _messageBus;
        private readonly KafkaGroupConsumer _groupConsumer;
        private readonly MethodInfo _consumerInstanceOnHandleMethod;

        public TopicSubscriberInstances(SubscriberSettings settings, KafkaGroupConsumer groupConsumer, KafkaMessageBus messageBus)
        {
            Topic = settings.Topic;

            _messageBus = messageBus;
            _groupConsumer = groupConsumer;

            _consumerInstanceOnHandleMethod = settings.ConsumerType.GetMethod("OnHandle");
            _consumerInstances = ResolveInstances(settings, messageBus);
            _consumerQueue = new BufferBlock<object>();
            _consumerInstances.ForEach(x => _consumerQueue.Post(x));
        }

        private static List<object> ResolveInstances(SubscriberSettings settings, BaseMessageBus messageBus)
        {
            var subscribers = new List<object>();
            for (var i = 0; i < settings.Instances; i++)
            {
                var subscriber = messageBus.Settings.DependencyResolver.Resolve(settings.ConsumerType).ToList();

                Assert.IsFalse(subscriber.Count == 0,
                    () => new ConfigurationMessageBusException($"There was no implementation of {settings.ConsumerType} returned by the resolver. Ensure you have registered an implementation for {settings.ConsumerType} in your DI container."));

                Assert.IsFalse(subscriber.Count > 1,
                    () => new ConfigurationMessageBusException($"More than one implementation of {settings.ConsumerType} returned by the resolver. Ensure you have registered exactly one implementation for {settings.ConsumerType} in your DI container."));

                subscribers.Add(subscriber[0]);
            }
            return subscribers;
        }

        public void EnqueueMessage(Message msg)
        {
            // ToDo: add timer trigger
            if (_messages.Count >= 10)
            {
                Log.DebugFormat("Reached {0} message threshold - will commit at offset {1}", 10, msg.TopicPartitionOffset);
                Commit(msg.TopicPartitionOffset);
            }

            _messages.Enqueue(new MessageProcessingResult(ProcessMessage(msg), msg));
        }

        private async Task ProcessMessage(Message msg)
        {
            var message = _messageBus.Settings.Serializer.Deserialize(_groupConsumer.MessageType, msg.Payload);

            var obj = await _consumerQueue.ReceiveAsync(_messageBus.CancellationToken);
            try
            {
                //var subscriber = (ISubscriber<object>)obj;
                //await subscriber.OnHandle(message, msg.Topic);

                var task = (Task) _consumerInstanceOnHandleMethod.Invoke(obj, new[] {message, msg.Topic});
                await task;
            }
            finally
            {
                await _consumerQueue.SendAsync(obj);
            }
        }

        #region IDisposable

        public void Dispose()
        {
            foreach (var subscriber in _consumerInstances)
            {
                var dispoable = subscriber as IDisposable;
                dispoable?.Dispose();
            }
            _consumerInstances.Clear();
        }

        #endregion

        public void Commit(TopicPartitionOffset offset)
        {
            try
            {
                Task.WaitAll(_messages.Select(x => x.Task).ToArray());
            }
            catch (AggregateException e)
            {
                Log.ErrorFormat("Errors occured while executing the tasks {0}", e);
                
                // ToDo
                // some tasks failed

                //_messages.OrderBy(x => x.Message.)

            }
            _groupConsumer.Commit(offset).Wait();
            _messages.Clear();
        }
    }
}