namespace SlimMessageBus.Host.Kafka
{
    using System.Diagnostics.CodeAnalysis;
    using Confluent.Kafka;
    using SlimMessageBus.Host.Config;
    using SlimMessageBus.Host.Serialization;
    using ConsumeResult = Confluent.Kafka.ConsumeResult<Confluent.Kafka.Ignore, byte[]>;

    /// <summary>
    /// Processor for regular consumers. 
    /// See also <see cref="IKafkaPartitionConsumer"/>.
    /// </summary>
    public class KafkaPartitionConsumerForConsumers : KafkaPartitionConsumer
    {
        public KafkaPartitionConsumerForConsumers(ConsumerSettings consumerSettings, TopicPartition topicPartition, IKafkaCommitController commitController, [NotNull] MessageBusBase messageBus, [NotNull] IMessageSerializer headerSerializer)
            : base(consumerSettings,
                   topicPartition,
                   commitController,
                   messageBus,
                   new ConsumerInstanceMessageProcessor<ConsumeResult>(consumerSettings, messageBus, m => m.ToMessageWithHeaders(headerSerializer), (m, ctx) => ctx.SetTransportMessage(m)))
        {
        }
    }
}