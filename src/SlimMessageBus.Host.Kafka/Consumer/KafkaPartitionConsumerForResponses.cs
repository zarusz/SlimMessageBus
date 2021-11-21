namespace SlimMessageBus.Host.Kafka
{
    using Confluent.Kafka;
    using SlimMessageBus.Host.Config;
    using SlimMessageBus.Host.Serialization;
    using ConsumeResult = Confluent.Kafka.ConsumeResult<Confluent.Kafka.Ignore, byte[]>;

    /// <summary>
    /// Processor for incomming response messages in the request-response patterns. 
    /// See also <see cref="IKafkaPartitionConsumer"/>.
    /// </summary>
    public class KafkaPartitionConsumerForResponses : KafkaPartitionConsumer
    {
        public KafkaPartitionConsumerForResponses(RequestResponseSettings requestResponseSettings, TopicPartition topicPartition, IKafkaCommitController commitController, MessageBusBase messageBus, IMessageSerializer headerSerializer)
            : base(requestResponseSettings, topicPartition, commitController, messageBus,
                  new ResponseMessageProcessor<ConsumeResult>(requestResponseSettings, messageBus, m => m.ToMessageWithHeaders(headerSerializer)))
        {
        }
    }
}