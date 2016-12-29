using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Logging;
using RdKafka;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Provider.Kafka
{
    public class MessageProcessingResult
    {
        public readonly Task Task;
        public readonly Message Message;

        public MessageProcessingResult(Task task, Message message)
        {
            Task = task;
            Message = message;
        }
    }

    public class KafkaGroupConsumer : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger<KafkaGroupConsumer>();

        public readonly KafkaMessageBus MessageBus;
        public readonly string Group;
        public readonly Type MessageType;
        public readonly Type SubscriberType;

        private EventConsumer _consumer;
        private readonly IDictionary<string, TopicSubscriberInstances> _consumerInstancesByTopic;

        public KafkaGroupConsumer(KafkaMessageBus messageBus, string group, Type messageType, ICollection<SubscriberSettings> groupSubscriberSettings)
        {
            MessageBus = messageBus;
            Group = group;
            MessageType = messageType;
            SubscriberType = typeof(ISubscriber<>).MakeGenericType(messageType);

            _consumerInstancesByTopic = groupSubscriberSettings
                .ToDictionary(x => x.Topic, x => new TopicSubscriberInstances(x, this, MessageBus));

            var config = new Config()
            {
                GroupId = group,
                EnableAutoCommit = false
            };
            _consumer = new EventConsumer(config, MessageBus.KafkaSettings.BrokerList);
            _consumer.OnMessage += OnMessage;
            _consumer.OnEndReached += OnEndReached;

            var topics = groupSubscriberSettings.Select(x => x.Topic).ToList();
            if (Log.IsInfoEnabled)
            {
                Log.InfoFormat("Subscribing to topics {0}, expecting message type {1}", string.Join(",", topics), MessageType);
            }
            _consumer.Subscribe(topics);
            _consumer.Start();
        }

        public Task Commit(TopicPartitionOffset offset)
        {
            return _consumer.Commit(new List<TopicPartitionOffset> {offset});
        }

        private void OnEndReached(object sender, TopicPartitionOffset offset)
        {
            
            Log.DebugFormat("Group {0}: Reached end of topic: {1} and parition: {2}, next message will be at offset: {3}", Group, offset.Topic, offset.Partition, offset.Offset);
            var consumerInstances = _consumerInstancesByTopic[offset.Topic];
            consumerInstances.Commit(offset);
        }

        private void OnMessage(object sender, Message msg)
        {
            try
            {
                Log.DebugFormat("Group {0}: Received message on topic: {1} (offset: {2}, payload size: {3})", Group, msg.Topic, msg.TopicPartitionOffset, msg.Payload.Length);
                var consumerInstances = _consumerInstancesByTopic[msg.Topic];
                consumerInstances.EnqueueMessage(msg);
            }
            catch (Exception e)
            {
                Log.ErrorFormat("Group {0}: Error occured while processing a message from topic {0} of type {1}. {2}", Group, msg.Topic, MessageType, e);
                throw;
            }
        }

        #region Implementation of IDisposable

        public void Dispose()
        {
            // first stop the consumer, so that messages do not get consumed at this point
            _consumer?.Stop().Wait();

            // dispose all subscriber instances
            foreach (var consumerInstances in _consumerInstancesByTopic.Values)
            {
                consumerInstances.Dispose();
            }
            _consumerInstancesByTopic.Clear();

            // dispose the consumer
            if (_consumer != null)
            {
                _consumer.Dispose();
                _consumer = null;
            }
        }

        #endregion
    }
}