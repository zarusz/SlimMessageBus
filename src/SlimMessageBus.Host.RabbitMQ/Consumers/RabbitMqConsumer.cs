namespace SlimMessageBus.Host.RabbitMQ;

using Microsoft.Extensions.DependencyInjection;

public interface IRabbitMqConsumer
{
    string Path { get; }
    void ConfirmMessage(BasicDeliverEventArgs transportMessage, RabbitMqMessageConfirmOptions option, IDictionary<string, object> properties, bool warnIfAlreadyConfirmed = false);
}

public class RabbitMqConsumer : AbstractRabbitMqConsumer, IRabbitMqConsumer
{
    public static readonly string ContextProperty_MessageConfirmed = "RabbitMq_MessageConfirmed";

    private readonly RabbitMqMessageAcknowledgementMode _acknowledgementMode;
    private readonly IMessageProcessor<BasicDeliverEventArgs> _messageProcessor;
    private readonly RoutingKeyMatcherService<IMessageProcessor<BasicDeliverEventArgs>> _routingKeyMatcher;

    private readonly RabbitMqMessageUnrecognizedRoutingKeyHandler _messageUnrecognizedRoutingKeyHandler;

    protected override RabbitMqMessageAcknowledgementMode AcknowledgementMode => _acknowledgementMode;

    public RabbitMqConsumer(
        ILoggerFactory loggerFactory,
        IRabbitMqChannel channel,
        string queueName,
        IList<ConsumerSettings> consumers,
        MessageBusBase<RabbitMqMessageBusSettings> messageBus,
        MessageProvider<BasicDeliverEventArgs> messageProvider,
        IHeaderValueConverter headerValueConverter)
        : base(loggerFactory.CreateLogger<RabbitMqConsumer>(),
               consumers,
               messageBus.Settings.ServiceProvider.GetServices<IAbstractConsumerInterceptor>(),
               channel,
               queueName,
               headerValueConverter)
    {
        _messageUnrecognizedRoutingKeyHandler = messageBus.ProviderSettings.MessageUnrecognizedRoutingKeyHandler;
        _acknowledgementMode = consumers.Select(x => x.GetOrDefault(RabbitMqProperties.MessageAcknowledgementMode, messageBus.Settings)).FirstOrDefault(x => x != null)
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

        var routingKeyProcessors = consumers
            .GroupBy(x => x.GetBindingRoutingKey() ?? string.Empty)
            .ToDictionary(x => x.Key, CreateMessageProcessor);

        // Initialize routing key matcher service
        _routingKeyMatcher = new RoutingKeyMatcherService<IMessageProcessor<BasicDeliverEventArgs>>(routingKeyProcessors);

        // Set single processor optimization if only one exact match exists and no wildcards
        _messageProcessor = routingKeyProcessors.Count == 1 && routingKeyProcessors.TryGetValue(string.Empty, out var value)
            ? value : null;
    }

    private IMessageProcessor<BasicDeliverEventArgs> FindMessageProcessor(string messageRoutingKey)
    {
        // If only single processor for all messages, return it directly for better performance
        if (_messageProcessor != null)
        {
            return _messageProcessor;
        }

        // Use routing key matcher service to find the appropriate processor
        return _routingKeyMatcher.FindMatch(messageRoutingKey);
    }

    protected override async Task OnStop()
    {
        try
        {
            // Wait max 5 seconds for all background processing tasks to complete
            using var taskCancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var backgroundProcessingTasks = _routingKeyMatcher.AllItems
                .Distinct()
                .OfType<ConcurrentMessageProcessorDecorator<BasicDeliverEventArgs>>()
                .Select(x => x.WaitAll(taskCancellationSource.Token));

            await Task.WhenAll(backgroundProcessingTasks);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Error occurred while waiting for background processing tasks to complete");
        }

        await base.OnStop();
    }

    internal void InitializeConsumerContext(BasicDeliverEventArgs transportMessage, ConsumerContext consumerContext)
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
                Logger.LogWarning("Exchange {Exchange} - Queue {Queue}: The message (delivery tag {MessageDeliveryTag}) was already confirmed, subsequent message confirmation will have no effect", transportMessage.Exchange, Path, transportMessage.DeliveryTag);
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

        var messageProcessor = FindMessageProcessor(transportMessage.RoutingKey);
        if (messageProcessor != null)
        {
            await messageProcessor.ProcessMessage(transportMessage, messageHeaders: messageHeaders, consumerContextProperties: consumerContextProperties, cancellationToken: CancellationToken);
        }
        else
        {
            Logger.LogDebug("Exchange {Exchange} - Queue {Queue}: No message processor found for routing key {RoutingKey}", transportMessage.Exchange, Path, transportMessage.RoutingKey);
            var confirmAction = _messageUnrecognizedRoutingKeyHandler(transportMessage);
            ConfirmMessage(transportMessage, confirmAction, messageHeaders);
        }

        // error handling happens in the message processor
        return null;
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        await base.DisposeAsyncCore();

        foreach (var messageProcessor in _routingKeyMatcher.AllItems.Distinct())
        {
            if (messageProcessor is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
