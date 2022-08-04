namespace SlimMessageBus.Host.Kafka;

using System.Diagnostics.CodeAnalysis;
using Confluent.Kafka;
using ConsumeResult = Confluent.Kafka.ConsumeResult<Confluent.Kafka.Ignore, byte[]>;

/// <summary>
/// The processor of assigned partition (<see cref="TopicPartition"/>).
/// </summary>
public interface IKafkaPartitionConsumer : IAsyncDisposable
{        
    TopicPartition TopicPartition { get; }

    void OnPartitionAssigned([NotNull] TopicPartition partition);
    Task OnMessage([NotNull] ConsumeResult message);
    void OnPartitionEndReached([NotNull] TopicPartitionOffset offset);
    void OnPartitionRevoked();

    void OnClose();
}