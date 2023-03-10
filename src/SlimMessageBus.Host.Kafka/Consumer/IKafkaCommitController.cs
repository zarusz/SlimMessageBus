namespace SlimMessageBus.Host.Kafka;

public interface IKafkaCommitController
{
    void Commit(TopicPartitionOffset offset);
}