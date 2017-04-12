using System;
using System.Collections.Generic;
using System.Linq;
using Common.Logging;
using Confluent.Kafka;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.Kafka
{
    public class KafkaGroupConsumer : KafkaGroupConsumerBase
    {
        private static readonly ILog Log = LogManager.GetLogger<KafkaGroupConsumer>();

        public readonly Type MessageType;

        private readonly IDictionary<string, TopicConsumerInstances> _consumerInstancesByTopic;

        public KafkaGroupConsumer(KafkaMessageBus messageBus, string group, Type messageType, ICollection<ConsumerSettings> groupSubscriberSettings)
            : base(messageBus, group, groupSubscriberSettings.Select(x => x.Topic).ToList())
        {
            Log.InfoFormat("Creating consumer for topics {0} and group {1}", string.Join(",", groupSubscriberSettings.Select(x => x.Topic)), group);

            MessageType = messageType;

            _consumerInstancesByTopic = groupSubscriberSettings
                .ToDictionary(x => x.Topic, x => new TopicConsumerInstances(x, this, MessageBus));

            Start();
        }

        protected override void OnPartitionEndReached(object sender, TopicPartitionOffset offset)
        {            
            base.OnPartitionEndReached(sender, offset);

            var consumerInstances = _consumerInstancesByTopic[offset.Topic];
            consumerInstances.Commit(offset);
        }

        protected override void OnMessage(object sender, Message msg)
        {
            base.OnMessage(sender, msg);

            try
            {
                var consumerInstances = _consumerInstancesByTopic[msg.Topic];
                consumerInstances.EnqueueMessage(msg);
            }
            catch (Exception e)
            {
                Log.ErrorFormat("Group [{0}]: Error occured while processing a message from topic {0} of type {1}. {2}", Group, msg.Topic, MessageType, e);
                throw;
            }
        }

        #region Implementation of IDisposable

        public override void Dispose()
        {
            // first stop the consumer, so that messages do not get consumed at this point
            Stop();

            // dispose all subscriber instances
            foreach (var consumerInstances in _consumerInstancesByTopic.Values)
            {
                consumerInstances.DisposeSilently("consumer instances", Log);
            }
            _consumerInstancesByTopic.Clear();

            base.Dispose();
        }

        #endregion
    }
}