namespace SlimMessageBus.Host.AmazonSQS;

internal class SqsQueueConsumer(
    SqsMessageBus messageBus,
    string path,
    ISqsClientProvider clientProvider,
    IMessageProcessor<SqsTransportMessageWithPayload> messageProcessor,
    IMessageSerializer<string> messageSerializer,
    IEnumerable<AbstractConsumerSettings> consumerSettings)
    : SqsBaseConsumer(messageBus,
          clientProvider,
          path,
          messageProcessor,
          messageSerializer,
          consumerSettings,
          messageBus.LoggerFactory.CreateLogger<SqsQueueConsumer>())
{
}
