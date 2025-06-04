namespace SlimMessageBus.Host.Kafka;

using ConsumeResult = ConsumeResult<Ignore, byte[]>;

/// <summary>
/// Processor for regular consumers. 
/// See also <see cref="IKafkaPartitionConsumer"/>.
/// </summary>
public class KafkaPartitionConsumerForConsumers : KafkaPartitionConsumer
{
    public KafkaPartitionConsumerForConsumers(ILoggerFactory loggerFactory,
                                              ConsumerSettings[] consumerSettings,
                                              string group,
                                              TopicPartition topicPartition,
                                              IKafkaCommitController commitController,
                                              IMessageSerializer headerSerializer,
                                              MessageProvider<ConsumeResult> messageProvider,
                                              MessageBusBase messageBus)
        : base(
            loggerFactory,
            consumerSettings,
            group,
            topicPartition,
            commitController,
            headerSerializer,
            new MessageProcessor<ConsumeResult>(
                consumerSettings,
                messageBus,
                path: topicPartition.Topic,
                responseProducer: messageBus,
                messageProvider: messageProvider,
                consumerContextInitializer: (m, ctx) => ctx.SetTransportMessage(m),
                consumerErrorHandlerOpenGenericType: typeof(IKafkaConsumerErrorHandler<>)))
    {
    }
}