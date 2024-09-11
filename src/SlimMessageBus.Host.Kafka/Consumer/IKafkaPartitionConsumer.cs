namespace SlimMessageBus.Host.Kafka;

using ConsumeResult = Confluent.Kafka.ConsumeResult<Confluent.Kafka.Ignore, byte[]>;

/// <summary>
/// The processor of assigned partition (<see cref="TopicPartition"/>).
/// </summary>
public interface IKafkaPartitionConsumer : IDisposable
{
    TopicPartition TopicPartition { get; }

    void OnPartitionAssigned(TopicPartition partition);
    Task OnMessage(ConsumeResult message);
    void OnPartitionEndReached();
    void OnPartitionRevoked();

    void OnClose();
}