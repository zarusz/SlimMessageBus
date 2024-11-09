namespace SlimMessageBus.Host.RabbitMQ;

using Microsoft.Extensions.Logging;

public class RabbitMqConsumer : AbstractRabbitMqConsumer
{
    public static readonly string ContextProperty_MessageConfirmed = "RabbitMq_MessageConfirmed";

    private readonly RabbitMqMessageAcknowledgementMode _acknowledgementMode;
    private readonly IMessageProcessor<BasicDeliverEventArgs> _messageProcessor;

    protected override RabbitMqMessageAcknowledgementMode AcknowledgementMode => _acknowledgementMode;

    public RabbitMqConsumer(
        ILoggerFactory loggerFactory,
        IRabbitMqChannel channel,
        string queueName,
        IList<ConsumerSettings> consumers,
        IMessageSerializer serializer,
        MessageBusBase messageBus,
        MessageProvider<BasicDeliverEventArgs> messageProvider,
        IHeaderValueConverter headerValueConverter)
        : base(loggerFactory.CreateLogger<RabbitMqConsumer>(), channel, queueName, headerValueConverter)
    {
        _acknowledgementMode = consumers.Select(x => x.GetOrDefault<RabbitMqMessageAcknowledgementMode?>(RabbitMqProperties.MessageAcknowledgementMode, messageBus.Settings)).FirstOrDefault(x => x != null)
            ?? RabbitMqMessageAcknowledgementMode.ConfirmAfterMessageProcessingWhenNoManualConfirmMade; // be default choose the safer acknowledgement mode
        _messageProcessor = new MessageProcessor<BasicDeliverEventArgs>(
            consumers,
            messageBus,
            path: queueName,
            responseProducer: messageBus,
            messageProvider: messageProvider,
            consumerContextInitializer: InitializeConsumerContext,
            consumerErrorHandlerOpenGenericType: typeof(IRabbitMqConsumerErrorHandler<>));
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

    private void ConfirmMessage(BasicDeliverEventArgs transportMessage, RabbitMqMessageConfirmOptions option, IDictionary<string, object> properties, bool warnIfAlreadyConfirmed = false)
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

        if ((option & RabbitMqMessageConfirmOptions.Ack) != 0)
        {
            AckMessage(transportMessage);
            confirmed = true;
        }
        else if ((option & RabbitMqMessageConfirmOptions.Nack) != 0)
        {
            NackMessage(transportMessage, requeue: (option & RabbitMqMessageConfirmOptions.Requeue) != 0);
            confirmed = true;
        }

        if (confirmed != null)
        {
            // mark the message has already been confirmed
            properties[ContextProperty_MessageConfirmed] = confirmed;
        }
    }

    protected override async Task<Exception> OnMessageReceived(Dictionary<string, object> messageHeaders, BasicDeliverEventArgs transportMessage)
    {
        var consumerContextProperties = new Dictionary<string, object>();

        if (_acknowledgementMode == RabbitMqMessageAcknowledgementMode.AckMessageBeforeProcessing)
        {
            // Acknowledge before processing
            ConfirmMessage(transportMessage, RabbitMqMessageConfirmOptions.Ack, consumerContextProperties);
        }

        var r = await _messageProcessor.ProcessMessage(transportMessage, messageHeaders: messageHeaders, consumerContextProperties: consumerContextProperties, cancellationToken: CancellationToken);

        if (_acknowledgementMode == RabbitMqMessageAcknowledgementMode.ConfirmAfterMessageProcessingWhenNoManualConfirmMade)
        {
            // Acknowledge after processing
            var confirmOption = r.Exception != null
                ? RabbitMqMessageConfirmOptions.Nack // NAck after processing when message fails (unless the user already acknowledged in any way).
                : RabbitMqMessageConfirmOptions.Ack; // Acknowledge after processing
            ConfirmMessage(transportMessage, confirmOption, consumerContextProperties);
        }

        if (r.Exception != null)
        {
            // We rely on the IMessageProcessor to execute the ConsumerErrorHandler<T>, but if it's not registered in the DI, it fails, or there is another fatal error then the message will be lost.
            Logger.LogError(r.Exception, "Error processing message {Message} from exchange {Exchange}, delivery tag {DeliveryTag}", transportMessage, transportMessage.Exchange, transportMessage.DeliveryTag);
        }

        return null;
    }
}
