namespace SlimMessageBus.Host.AmazonSQS;

public class SqsQueueConsumer(
    SqsMessageBus messageBus,
    string path,
    ISqsClientProvider clientProvider,
    IMessageProcessor<Message> messageProcessor,
    IEnumerable<AbstractConsumerSettings> consumerSettings)
    : SqsBaseConsumer(messageBus,
          clientProvider,
          path,
          messageProcessor,
          consumerSettings,
          messageBus.LoggerFactory.CreateLogger<SqsQueueConsumer>())
{
}
