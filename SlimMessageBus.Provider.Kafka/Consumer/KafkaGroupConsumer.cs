using System;
using System.Collections.Generic;
using System.Linq;
using Common.Logging;
using RdKafka;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Provider.Kafka
{
    public class KafkaGroupConsumer : KafkaGroupConsumerBase
    {
        private static readonly ILog Log = LogManager.GetLogger<KafkaGroupConsumer>();

        public readonly Type MessageType;

        private readonly IDictionary<string, TopicConsumerInstances> _consumerInstancesByTopic;

        public KafkaGroupConsumer(KafkaMessageBus messageBus, string group, Type messageType, ICollection<SubscriberSettings> groupSubscriberSettings)
            : base(messageBus, group, groupSubscriberSettings.Select(x => x.Topic).ToList())
        {
            Log.InfoFormat("Creating consumer for topics {0} and group {1}", string.Join(",", groupSubscriberSettings.Select(x => x.Topic)), group);

            MessageType = messageType;

            _consumerInstancesByTopic = groupSubscriberSettings
                .ToDictionary(x => x.Topic, x => new TopicConsumerInstances(x, this, MessageBus));

            Consumer.Start();
        }

        protected override void OnEndReached(object sender, TopicPartitionOffset offset)
        {            
            Log.DebugFormat("Group {0}: Reached end of topic: {1} and parition: {2}, next message will be at offset: {3}", Group, offset.Topic, offset.Partition, offset.Offset);
            var consumerInstances = _consumerInstancesByTopic[offset.Topic];
            consumerInstances.Commit(offset);
        }

        protected override void OnMessage(object sender, Message msg)
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

        public override void Dispose()
        {
            // first stop the consumer, so that messages do not get consumed at this point
            Consumer?.Stop().Wait();

            // dispose all subscriber instances
            foreach (var consumerInstances in _consumerInstancesByTopic.Values)
            {
                consumerInstances.DisposeSilently(e => Log.WarnFormat("Error occured while disposing consumer instances. {0}", e));
            }
            _consumerInstancesByTopic.Clear();

            base.Dispose();
        }

        #endregion
    }
}