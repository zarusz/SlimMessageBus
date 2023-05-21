namespace SlimMessageBus.Host.Kafka;

using ConsumeResult = ConsumeResult<Ignore, byte[]>;

/// <summary>
/// Processor for incomming response messages in the request-response patterns. 
/// See also <see cref="IKafkaPartitionConsumer"/>.
/// </summary>
public class KafkaPartitionConsumerForResponses : KafkaPartitionConsumer
{
    public KafkaPartitionConsumerForResponses(ILoggerFactory loggerFactory, RequestResponseSettings requestResponseSettings, string group, TopicPartition topicPartition, IKafkaCommitController commitController, IResponseConsumer responseConsumer, IMessageSerializer headerSerializer)
        : base(
            loggerFactory, 
            new[] { requestResponseSettings }, 
            group, 
            topicPartition, 
            commitController, 
            headerSerializer,
            messageProcessor: new ResponseMessageProcessor<ConsumeResult>(
                loggerFactory, 
                requestResponseSettings, 
                responseConsumer,
                messagePayloadProvider: m => m.Message.Value))
    {
    }
}