namespace SlimMessageBus.Host.Memory;

using System.Runtime.ExceptionServices;

/// <summary>
/// In-memory message bus <see cref="IMessageBus"/> implementation to use for in process message passing.
/// </summary>
public partial class MemoryMessageBus : MessageBusBase<MemoryMessageBusSettings>
{
    private readonly ILogger _logger;
    private IDictionary<string, IMessageProcessor<object>> _messageProcessorByPath;
    private IDictionary<string, IMessageProcessorQueue> _messageProcessorQueueByPath;

    public MemoryMessageBus(MessageBusSettings settings, MemoryMessageBusSettings providerSettings)
        : base(settings, providerSettings)
    {
        _logger = LoggerFactory.CreateLogger<MemoryMessageBus>();

        OnBuildProvider();
    }

    #region Overrides of MessageBusBase

    protected override async ValueTask DisposeAsyncCore()
    {
        await base.DisposeAsyncCore();

        foreach (var d in _messageProcessorQueueByPath.Values.OfType<IDisposable>())
        {
            d.Dispose();
        }
        _messageProcessorQueueByPath.Clear();

        foreach (var d in _messageProcessorByPath.Values.OfType<IDisposable>())
        {
            d.Dispose();
        }
        _messageProcessorByPath.Clear();
    }

    protected override IMessageSerializerProvider GetSerializerProvider()
    {
        if (ProviderSettings.EnableMessageSerialization)
        {
            return base.GetSerializerProvider();
        }
        // No serialization
        return new NullMessageSerializerProvider();
    }

    public override IDictionary<string, object> CreateHeaders()
    {
        if (ProviderSettings.EnableMessageHeaders)
        {
            return base.CreateHeaders();
        }
        // Do not use headers
        return null;
    }

    // Memory bus does not require requestId
    protected override string GenerateRequestId() => null;

    public override bool IsMessageScopeEnabled(ConsumerSettings consumerSettings, IDictionary<string, object> consumerContextProperties)
    {
        if (consumerSettings is null) throw new ArgumentNullException(nameof(consumerSettings));

        if (consumerContextProperties != null && consumerContextProperties.ContainsKey(MemoryMessageBusProperties.CreateScope))
        {
            return true;
        }

        return consumerSettings.IsMessageScopeEnabled
                ?? Settings.IsMessageScopeEnabled
                ?? false; // by default Memory Bus has scoped message disabled
    }

    protected override void Build()
    {
        base.Build();

        static int ConsumerModeOrder(ConsumerSettings settings) => settings.ConsumerMode == ConsumerMode.Consumer ? 0 : 1;

        _messageProcessorByPath = Settings.Consumers
            .GroupBy(x => x.Path)
            .ToDictionary(
                x => x.Key,
                // Note: The consumers will first have IConsumer<>, then IRequestHandler<>
                x => CreateMessageProcessor([.. x.OrderBy(consumerSettings => ConsumerModeOrder(consumerSettings))], x.Key));

        _messageProcessorQueueByPath = ProviderSettings.EnableBlockingPublish
            ? []
            : _messageProcessorByPath.ToDictionary(x => x.Key, x => CreateMessageProcessorQueue(x.Value));
    }

    private IMessageProcessor<object> CreateMessageProcessor(IEnumerable<ConsumerSettings> consumerSettings, string path)
    {
        var messageSerializer = SerializerProvider.GetSerializer(path);
        return new MessageProcessor<object>(
                consumerSettings,
                this,
                path: path,
                responseProducer: null,
                messageProvider: ProviderSettings.EnableMessageSerialization
                    ? (messageType, transportMessage) => messageSerializer.Deserialize(messageType, (byte[])transportMessage)
                    : (messageType, transportMessage) => transportMessage,
                messageTypeProvider: ProviderSettings.EnableMessageSerialization
                    ? null
                    : transportMessage => transportMessage.GetType(),
                consumerErrorHandlerOpenGenericType: typeof(IMemoryConsumerErrorHandler<>));
    }

    private IMessageProcessorQueue CreateMessageProcessorQueue(IMessageProcessor<object> messageProcessor)
    {
        var concurrency = messageProcessor.ConsumerSettings.Select(x => x.Instances).Max();
        return concurrency > 1
            ? new ConcurrentMessageProcessorQueue(messageProcessor, LoggerFactory.CreateLogger<ConcurrentMessageProcessorQueue>(), concurrency, CancellationToken)
            : new MessageProcessorQueue(messageProcessor, LoggerFactory.CreateLogger<MessageProcessorQueue>(), CancellationToken);
    }

    public override Task ProduceToTransport(object message, Type messageType, string path, IDictionary<string, object> messageHeaders, IMessageBusTarget targetBus, CancellationToken cancellationToken)
        => ProduceInternal<object>(message, path, messageHeaders, targetBus, isPublish: true, cancellationToken);

    protected override Task<TResponseMessage> SendInternal<TResponseMessage>(object request, string path, Type requestType, Type responseType, ProducerSettings producerSettings, DateTimeOffset created, DateTimeOffset expires, string requestId, IDictionary<string, object> requestHeaders, IMessageBusTarget targetBus, CancellationToken cancellationToken)
        => ProduceInternal<TResponseMessage>(request, path, requestHeaders, targetBus, isPublish: false, cancellationToken);

    #endregion

    private async Task<TResponseMessage> ProduceInternal<TResponseMessage>(object message, string path, IDictionary<string, object> requestHeaders, IMessageBusTarget targetBus, bool isPublish, CancellationToken cancellationToken)
    {
        var messageType = message.GetType();
        var producerSettings = GetProducerSettings(messageType);
        path ??= GetDefaultPath(producerSettings.MessageType, producerSettings);
        if (!_messageProcessorByPath.TryGetValue(path, out var messageProcessor))
        {
            LogNoConsumerInterestedInMessageType(path, messageType);
            return default;
        }

        var transportMessage = ProviderSettings.EnableMessageSerialization
            ? SerializerProvider.GetSerializer(path).Serialize(producerSettings.MessageType, message)
            : message;

        var messageHeadersReadOnly = requestHeaders != null
            ? requestHeaders as IReadOnlyDictionary<string, object> ?? new Dictionary<string, object>(requestHeaders)
            : null;

        if (isPublish && !ProviderSettings.EnableBlockingPublish)
        {
            // Execute the message processor in asynchronous manner
            if (_messageProcessorQueueByPath.TryGetValue(path, out var messageProcessorQueue))
            {
                messageProcessorQueue.Enqueue(transportMessage, messageHeadersReadOnly);
            }
            return default;
        }

        var serviceProvider = targetBus?.ServiceProvider ?? Settings.ServiceProvider;
        // Execute the message processor in synchronous manner
        var r = await messageProcessor.ProcessMessage(transportMessage, messageHeadersReadOnly, currentServiceProvider: serviceProvider, cancellationToken: cancellationToken);
        if (r.Exception != null)
        {
            // We want to pass the same exception to the sender as it happened in the handler/consumer
            ExceptionDispatchInfo.Capture(r.Exception).Throw();
        }
        return (TResponseMessage)r.Response;
    }

    #region Logging

    [LoggerMessage(
       EventId = 0,
       Level = LogLevel.Debug,
       Message = "No consumers interested in message type {MessageType} on path {Path}")]
    private partial void LogNoConsumerInterestedInMessageType(string path, Type messageType);

    #endregion
}

#if NETSTANDARD2_0

public partial class MemoryMessageBus
{
    private partial void LogNoConsumerInterestedInMessageType(string path, Type messageType)
        => _logger.LogDebug("No consumers interested in message type {MessageType} on path {Path}", messageType, path);
}

#endif