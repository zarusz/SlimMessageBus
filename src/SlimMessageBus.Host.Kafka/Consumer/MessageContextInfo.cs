using Confluent.Kafka;

namespace SlimMessageBus.Host.Kafka
{
    public class MessageContextInfo
    {
        public string Group { get; }
        public TopicPartitionOffset TopicPartitionOffset { get; }

        public MessageContextInfo(string group, TopicPartitionOffset tpo)
        {
            Group = group;
            TopicPartitionOffset = tpo;
        }

        #region Overrides of Object

        public override string ToString()
        {
            return $"Group: {Group}, Topic: {TopicPartitionOffset.Topic}, Partition: {TopicPartitionOffset.Partition}, Offset: {TopicPartitionOffset.Offset}";
        }

        #endregion
    }
}


