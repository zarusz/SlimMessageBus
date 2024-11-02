namespace SlimMessageBus.Host.Kafka;

public interface IKafkaCommitController
{
    /// <summary>
    /// The offset of the topic-parition that should be commited onto the consumer group
    /// </summary>
    /// <param name="offset"></param>
    void Commit(TopicPartitionOffset offset);
}