using System.Collections.Generic;
using Common.Logging;
using Confluent.Kafka;
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
            Start();
        }

        #region Overrides of KafkaGroupConsumerBase

        protected override void OnPartitionEndReached(object sender, TopicPartitionOffset offset)
        {
            base.OnPartitionEndReached(sender, offset);
            Commit(offset);
        }

        protected override void OnMessage(object sender, Message msg)
        {
            base.OnMessage(sender, msg);
            MessageBus.OnResponseArrived(msg.Value, MessageBus.Settings.RequestResponse.Topic).Wait();
        }

        #endregion

        public override void Dispose()
        {
            Stop();
            base.Dispose();
        }

    }
}