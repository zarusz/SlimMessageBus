using Confluent.Kafka;

namespace SlimMessageBus.Host.Kafka
{
    public interface IKafkaCommitController
    {
        void Commit(TopicPartitionOffset offset);
    }
}