namespace SlimMessageBus.Host.RabbitMQ;

public class RabbitMqConsumer : AbstractRabbitMqConsumer
{
    private readonly MessageBusBase _messageBus;
    private readonly IMessageProcessor<BasicDeliverEventArgs> _messageProcessor;

    public RabbitMqConsumer(ILoggerFactory loggerFactory, IRabbitMqChannel channel, string queueName, IList<ConsumerSettings> consumers, IMessageSerializer serializer, MessageBusBase messageBus, IHeaderValueConverter headerValueConverter)
        : base(loggerFactory.CreateLogger<RabbitMqConsumer>(), channel, queueName, headerValueConverter)
    {
        _messageBus = messageBus;
        _messageProcessor = new MessageProcessor<BasicDeliverEventArgs>(
            consumers,
            messageBus,
            path: queueName,
            responseProducer: messageBus,
            messageProvider: (messageType, m) => serializer.Deserialize(messageType, m.Body.ToArray()),
            consumerContextInitializer: InitializeConsumerContext);
    }

    private static void InitializeConsumerContext(BasicDeliverEventArgs m, ConsumerContext consumerContext) => consumerContext.SetTransportMessage(m);

    protected override async Task<Exception> OnMessageRecieved(Dictionary<string, object> messageHeaders, BasicDeliverEventArgs transportMessage)
    {
        var (exception, _, _, message) = await _messageProcessor.ProcessMessage(transportMessage, messageHeaders: messageHeaders, cancellationToken: CancellationToken);
        if (exception == null)
        {
            AckMessage(transportMessage);
        }
        else
        {
            await OnMessageError(transportMessage, messageHeaders, message, exception);
        }
        return null;
    }

    private async Task OnMessageError(BasicDeliverEventArgs @event, Dictionary<string, object> messageHeaders, object message, Exception exception)
    {
        var consumerContext = CreateConsumerContext(@event, messageHeaders);

        // Obtain from cache closed generic type IConsumerErrorHandler<TMessage> of the message type
        var consumerErrorHandlerType = _messageBus.RuntimeTypeCache.GetClosedGenericType(typeof(RabbitMqConsumerErrorHandler<>), message.GetType());
        // Resolve IConsumerErrorHandler<> of the message type
        var consumerErrorHandler = _messageBus.Settings.ServiceProvider.GetService(consumerErrorHandlerType) as IConsumerErrorHandlerUntyped;
        if (consumerErrorHandler != null)
        {
            var handled = await consumerErrorHandler.OnHandleError(message, consumerContext, exception);
            if (handled)
            {
                Logger.LogDebug("Error handling was performed by {ConsumerErrorHandlerType}", consumerErrorHandler.GetType().Name);
                return;
            }
        }

        // By default nack the message
        NackMessage(@event, requeue: false);
    }

    private ConsumerContext CreateConsumerContext(BasicDeliverEventArgs @event, Dictionary<string, object> messageHeaders)
    {
        var consumerContext = new ConsumerContext
        {
            Path = QueueName,
            CancellationToken = CancellationToken,
            Bus = _messageBus,
            Headers = messageHeaders,
            ConsumerInvoker = null,
            Consumer = null,
        };

        consumerContext.SetConfirmAction(option =>
        {
            if ((option & RabbitMqMessageConfirmOption.Ack) != 0)
            {
                AckMessage(@event);
            }
            else if ((option & RabbitMqMessageConfirmOption.Nack) != 0)
            {
                NackMessage(@event, requeue: (option & RabbitMqMessageConfirmOption.Requeue) != 0);
            }
        });

        InitializeConsumerContext(@event, consumerContext);

        return consumerContext;
    }
}
