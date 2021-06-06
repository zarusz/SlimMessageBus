namespace SlimMessageBus.Host.Kafka
{
    using Confluent.Kafka;

    public interface IKafkaCommitController
    {
        void Commit(TopicPartitionOffset offset);
    }
}