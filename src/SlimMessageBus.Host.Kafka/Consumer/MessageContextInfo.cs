using Confluent.Kafka;

namespace SlimMessageBus.Host.Kafka
{
    internal class MessageContextInfo
    {
        public string Group { get; }
        public TopicPartitionOffset TopicPartitionOffset { get; }

        public MessageContextInfo(string group, TopicPartitionOffset tpo)
        {
            Group = group;
            TopicPartitionOffset = tpo;
        }

        public override string ToString() => $"Group: {Group}, Topic: {TopicPartitionOffset.Topic}, Partition: {TopicPartitionOffset.Partition}, Offset: {TopicPartitionOffset.Offset}";
    }
}


