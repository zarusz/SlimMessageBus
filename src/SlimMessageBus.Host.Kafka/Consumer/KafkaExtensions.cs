namespace SlimMessageBus.Host.Kafka
{
    using Confluent.Kafka;
    using System.Diagnostics.CodeAnalysis;

    public static class KafkaExtensions
    {
        public static TopicPartitionOffset AddOffset([NotNull] this TopicPartitionOffset topicPartitionOffset, int addOffset)
            => new TopicPartitionOffset(topicPartitionOffset.TopicPartition, topicPartitionOffset.Offset + addOffset);
    }
}


