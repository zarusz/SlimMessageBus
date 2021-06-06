namespace SlimMessageBus.Host.Kafka
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;
    using Confluent.Kafka;
    using ConsumeResult = Confluent.Kafka.ConsumeResult<Confluent.Kafka.Ignore, byte[]>;

    /// <summary>
    /// The processor of assigned partition (<see cref="TopicPartition"/>).
    /// </summary>
    public interface IKafkaTopicPartitionProcessor : IDisposable
    {        
        TopicPartition TopicPartition { get; }

        ValueTask OnMessage([NotNull] ConsumeResult message);
        ValueTask OnPartitionEndReached([NotNull] TopicPartitionOffset offset);
        void OnPartitionRevoked();
        ValueTask OnClose();
    }
}