namespace SlimMessageBus.Host.Kafka;

using ConsumeResult = ConsumeResult<Ignore, byte[]>;

/// <summary>
/// Processor for incoming response messages in the request-response patterns. 
/// See also <see cref="IKafkaPartitionConsumer"/>.
/// </summary>
public class KafkaPartitionConsumerForResponses : KafkaPartitionConsumer
{
    public KafkaPartitionConsumerForResponses(
        ILoggerFactory loggerFactory,
        RequestResponseSettings requestResponseSettings,
        string group,
        TopicPartition topicPartition,
        IKafkaCommitController commitController,
        MessageProvider<ConsumeResult> messageProvider,
        IPendingRequestStore pendingRequestStore,
        TimeProvider timeProvider,
        IMessageSerializer headerSerializer)
        : base(
            loggerFactory,
            [requestResponseSettings],
            group,
            topicPartition,
            commitController,
            headerSerializer,
            messageProcessor: new ResponseMessageProcessor<ConsumeResult>(
                loggerFactory,
                requestResponseSettings,
                messageProvider,
                pendingRequestStore,
                timeProvider))
    {
    }
}