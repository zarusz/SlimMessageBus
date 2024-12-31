namespace SlimMessageBus.Host.RabbitMQ;

public interface IRabbitMqConsumer
{
    string QueueName { get; }
    void ConfirmMessage(BasicDeliverEventArgs transportMessage, RabbitMqMessageConfirmOptions option, IDictionary<string, object> properties, bool warnIfAlreadyConfirmed = false);
}

public class RabbitMqConsumer : AbstractRabbitMqConsumer, IRabbitMqConsumer
{
    public static readonly string ContextProperty_MessageConfirmed = "RabbitMq_MessageConfirmed";

    private readonly RabbitMqMessageAcknowledgementMode _acknowledgementMode;
    private readonly IMessageProcessor<BasicDeliverEventArgs> _messageProcessor;
    private readonly IDictionary<string, IMessageProcessor<BasicDeliverEventArgs>> _messageProcessorByRoutingKey;

    protected override RabbitMqMessageAcknowledgementMode AcknowledgementMode => _acknowledgementMode;

    public RabbitMqConsumer(
        ILoggerFactory loggerFactory,
        IRabbitMqChannel channel,
        string queueName,
        IList<ConsumerSettings> consumers,
        MessageBusBase messageBus,
        MessageProvider<BasicDeliverEventArgs> messageProvider,
        IHeaderValueConverter headerValueConverter)
        : base(loggerFactory.CreateLogger<RabbitMqConsumer>(), channel, queueName, headerValueConverter)
    {
        _acknowledgementMode = consumers.Select(x => x.GetOrDefault<RabbitMqMessageAcknowledgementMode?>(RabbitMqProperties.MessageAcknowledgementMode, messageBus.Settings)).FirstOrDefault(x => x != null)
            ?? RabbitMqMessageAcknowledgementMode.ConfirmAfterMessageProcessingWhenNoManualConfirmMade; // be default choose the safer acknowledgement mode

        IMessageProcessor<BasicDeliverEventArgs> CreateMessageProcessor(IEnumerable<ConsumerSettings> consumers)
        {
            IMessageProcessor<BasicDeliverEventArgs> messageProcessor = new MessageProcessor<BasicDeliverEventArgs>(
                consumers,
                messageBus,
                path: queueName,
                responseProducer: messageBus,
                messageProvider: messageProvider,
                consumerContextInitializer: InitializeConsumerContext,
                consumerErrorHandlerOpenGenericType: typeof(IRabbitMqConsumerErrorHandler<>));

            messageProcessor = new RabbitMqAutoAcknowledgeMessageProcessor(messageProcessor, Logger, _acknowledgementMode, this);

            // pick the maximum number of instances
            var instances = consumers.Max(x => x.Instances);
            // For a given rabbit channel, there is only 1 task that dispatches messages. We want to be able to let each SMB consume process within its own task (1 or more)
            messageProcessor = new ConcurrentMessageProcessorDecorator<BasicDeliverEventArgs>(instances, loggerFactory, messageProcessor);

            return messageProcessor;
        }

        _messageProcessorByRoutingKey = consumers
            .GroupBy(x => x.GetBindingRoutingKey() ?? string.Empty)
            .ToDictionary(x => x.Key, CreateMessageProcessor);

        _messageProcessor = _messageProcessorByRoutingKey.Count == 1 && _messageProcessorByRoutingKey.TryGetValue(string.Empty, out var value)
            ? value : null;
    }

    protected override async Task OnStop()
    {
        try
        {
            // Wait max 5 seconds for all background processing tasks to complete
            using var taskCancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var backgrounProcessingTasks = _messageProcessorByRoutingKey.Values
                .OfType<ConcurrentMessageProcessorDecorator<BasicDeliverEventArgs>>()
                .Select(x => x.WaitAll(taskCancellationSource.Token));

            await Task.WhenAll(backgrounProcessingTasks);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Error occurred while waiting for background processing tasks to complete");
        }

        await base.OnStop();
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

    public void ConfirmMessage(BasicDeliverEventArgs transportMessage, RabbitMqMessageConfirmOptions option, IDictionary<string, object> properties, bool warnIfAlreadyConfirmed = false)
    {
        if (properties.TryGetValue(ContextProperty_MessageConfirmed, out var confirmed) && confirmed is true)
        {
            // Note: We want to makes sure the 1st message confirmation is handled
            if (warnIfAlreadyConfirmed)
            {
                Logger.LogWarning("Exchange {Exchange} - Queue {Queue}: The message (delivery tag {MessageDeliveryTag}) was already confirmed, subsequent message confirmation will have no effect", transportMessage.Exchange, QueueName, transportMessage.DeliveryTag);
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

        var messageProcessor = _messageProcessor;
        if (messageProcessor != null || _messageProcessorByRoutingKey.TryGetValue(transportMessage.RoutingKey, out messageProcessor))
        {
            await messageProcessor.ProcessMessage(transportMessage, messageHeaders: messageHeaders, consumerContextProperties: consumerContextProperties, cancellationToken: CancellationToken);
        }
        else
        {
            Logger.LogDebug("Exchange {Exchange} - Queue {Queue}: No message processor found for routing key {RoutingKey}", transportMessage.Exchange, QueueName, transportMessage.RoutingKey);
        }

        // error handling happens in the message processor
        return null;
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        await base.DisposeAsyncCore();

        foreach (var messageProcessor in _messageProcessorByRoutingKey.Values)
        {
            if (messageProcessor is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
