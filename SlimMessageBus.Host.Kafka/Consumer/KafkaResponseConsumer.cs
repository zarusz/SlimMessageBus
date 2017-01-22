using System.Collections.Generic;
using Common.Logging;
using RdKafka;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.Kafka
{
    public class KafkaResponseConsumer : KafkaGroupConsumerBase
    {
        private static readonly ILog Log = LogManager.GetLogger<KafkaResponseConsumer>();

        public KafkaResponseConsumer(KafkaMessageBus messageBus)
            : this(messageBus, messageBus.Settings.RequestResponse)
        {
        }

        public KafkaResponseConsumer(KafkaMessageBus messageBus, RequestResponseSettings requestResponseSettings)
            : base(messageBus, requestResponseSettings.Group, new List<string> { requestResponseSettings.Topic })
        {
            Consumer.Start();
        }

        #region Overrides of KafkaGroupConsumerBase

        protected override void OnEndReached(object sender, TopicPartitionOffset offset)
        {
            Commit(offset);
        }

        protected override void OnMessage(object sender, Message msg)
        {
            MessageBus.OnResponseArrived(msg.Payload, MessageBus.Settings.RequestResponse.Topic).Wait();
        }

        #endregion
    }
}