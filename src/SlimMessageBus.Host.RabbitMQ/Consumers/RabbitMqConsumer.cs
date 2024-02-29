namespace SlimMessageBus.Host.RabbitMQ;

public class RabbitMqConsumer : AbstractRabbitMqConsumer
{
    public static readonly string ContextProperty_MessageConfirmed = "RabbitMq_MessageConfirmed";

    private readonly MessageBusBase _messageBus;
    private readonly RabbitMqMessageAcknowledgementMode _acknowledgementMode;
    private readonly IMessageProcessor<BasicDeliverEventArgs> _messageProcessor;

    protected override RabbitMqMessageAcknowledgementMode AcknowledgementMode => _acknowledgementMode;

    public RabbitMqConsumer(ILoggerFactory loggerFactory, IRabbitMqChannel channel, string queueName, IList<ConsumerSettings> consumers, IMessageSerializer serializer, MessageBusBase messageBus, IHeaderValueConverter headerValueConverter)
        : base(loggerFactory.CreateLogger<RabbitMqConsumer>(), channel, queueName, headerValueConverter)
    {
        _messageBus = messageBus;
        _acknowledgementMode = consumers.Select(x => x.GetOrDefault<RabbitMqMessageAcknowledgementMode?>(RabbitMqProperties.MessageAcknowledgementMode, messageBus.Settings)).FirstOrDefault(x => x != null)
            ?? RabbitMqMessageAcknowledgementMode.ConfirmAfterMessageProcessingWhenNoManualConfirmMade; // be default choose the safer acknowledgement mode
        _messageProcessor = new MessageProcessor<BasicDeliverEventArgs>(
            consumers,
            messageBus,
            path: queueName,
            responseProducer: messageBus,
            messageProvider: (messageType, m) => serializer.Deserialize(messageType, m.Body.ToArray()),
            consumerContextInitializer: InitializeConsumerContext);
    }

    private void InitializeConsumerContext(BasicDeliverEventArgs transportMessage, ConsumerContext consumerContext)
    {
        if (_acknowledgementMode == RabbitMqMessageAcknowledgementMode.AckAutomaticByRabbit)
        {
            // mark the message has already been confirmed when in automatic acknowledgment
            consumerContext.Properties[ContextProperty_MessageConfirmed] = true;
        }

        // provide transport message
        consumerContext.SetTransportMessage(transportMessage);
        // provide methods to confirm message
        consumerContext.SetConfirmAction(option => ConfirmMessage(transportMessage, option, consumerContext.Properties, warnIfAlreadyConfirmed: true));
    }

    private void ConfirmMessage(BasicDeliverEventArgs transportMessage, RabbitMqMessageConfirmOption option, IDictionary<string, object> properties, bool warnIfAlreadyConfirmed = false)
    {
        if (properties.TryGetValue(ContextProperty_MessageConfirmed, out var confirmed) && confirmed is true)
        {
            // Note: We want to makes sure the 1st message confirmation is handled
            if (warnIfAlreadyConfirmed)
            {
                Logger.LogWarning("The message (delivery tag {MessageDeliveryTag}, queue name {QueueName}) was already confirmed, subsequent message confirmation will have no effect", transportMessage.DeliveryTag, QueueName);
            }
            return;
        }

        if ((option & RabbitMqMessageConfirmOption.Ack) != 0)
        {
            AckMessage(transportMessage);
            confirmed = true;
        }
        else if ((option & RabbitMqMessageConfirmOption.Nack) != 0)
        {
            NackMessage(transportMessage, requeue: (option & RabbitMqMessageConfirmOption.Requeue) != 0);
            confirmed = true;
        }

        if (confirmed != null)
        {
            // mark the message has already been confirmed
            properties[ContextProperty_MessageConfirmed] = confirmed;
        }
    }

    protected override async Task<Exception> OnMessageRecieved(Dictionary<string, object> messageHeaders, BasicDeliverEventArgs transportMessage)
    {
        var consumerContextProperties = new Dictionary<string, object>();

        if (_acknowledgementMode == RabbitMqMessageAcknowledgementMode.AckMessageBeforeProcessing)
        {
            // Acknowledge before processing
            ConfirmMessage(transportMessage, RabbitMqMessageConfirmOption.Ack, consumerContextProperties);
        }

        var (exception, _, _, message) = await _messageProcessor.ProcessMessage(transportMessage, messageHeaders: messageHeaders, consumerContextProperties: consumerContextProperties, cancellationToken: CancellationToken);
        if (exception == null)
        {
            if (_acknowledgementMode == RabbitMqMessageAcknowledgementMode.ConfirmAfterMessageProcessingWhenNoManualConfirmMade)
            {
                // Acknowledge after processing
                ConfirmMessage(transportMessage, RabbitMqMessageConfirmOption.Ack, consumerContextProperties);
            }
        }
        else
        {
            await OnMessageError(transportMessage, messageHeaders, message, exception, consumerContextProperties);
        }
        return null;
    }

    private async Task OnMessageError(BasicDeliverEventArgs transportMessage, Dictionary<string, object> messageHeaders, object message, Exception exception, Dictionary<string, object> consumerContextProperties)
    {
        var consumerContext = CreateErrorConsumerContext(transportMessage, messageHeaders, consumerContextProperties);

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

        if (_acknowledgementMode == RabbitMqMessageAcknowledgementMode.ConfirmAfterMessageProcessingWhenNoManualConfirmMade)
        {
            // NAck after processing when message fails (unless the user already acknowledged in any way).
            ConfirmMessage(transportMessage, RabbitMqMessageConfirmOption.Nack, consumerContextProperties);
        }
    }

    private ConsumerContext CreateErrorConsumerContext(BasicDeliverEventArgs @event, Dictionary<string, object> messageHeaders, Dictionary<string, object> consumerContextProperties)
    {
        var consumerContext = new ConsumerContext(consumerContextProperties)
        {
            Path = QueueName,
            CancellationToken = CancellationToken,
            Bus = _messageBus,
            Headers = messageHeaders,
            ConsumerInvoker = null,
            Consumer = null,
        };

        InitializeConsumerContext(@event, consumerContext);

        return consumerContext;
    }
}
