namespace SlimMessageBus.Host.Kafka;

using SlimMessageBus.Host.Serialization;
using ConsumeResult = ConsumeResult<Ignore, byte[]>;

/// <summary>
/// Processor for incomming response messages in the request-response patterns. 
/// See also <see cref="IKafkaPartitionConsumer"/>.
/// </summary>
public class KafkaPartitionConsumerForResponses : KafkaPartitionConsumer
{
    public KafkaPartitionConsumerForResponses(RequestResponseSettings requestResponseSettings, string group, TopicPartition topicPartition, IKafkaCommitController commitController, MessageBusBase messageBus, IMessageSerializer headerSerializer)
        : base(new[] { requestResponseSettings }, group, topicPartition, commitController, messageBus, headerSerializer)
    {
    }

    protected override IMessageProcessor<ConsumeResult<Ignore, byte[]>> CreateMessageProcessor()
        => new ResponseMessageProcessor<ConsumeResult>((RequestResponseSettings)ConsumerSettings[0], MessageBus, m => m.Message.Value);
}