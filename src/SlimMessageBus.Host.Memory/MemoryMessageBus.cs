﻿namespace SlimMessageBus.Host.Memory;

using System.Runtime.ExceptionServices;

/// <summary>
/// In-memory message bus <see cref="IMessageBus"/> implementation to use for in process message passing.
/// </summary>
public class MemoryMessageBus : MessageBusBase<MemoryMessageBusSettings>
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

    protected override IMessageSerializer GetSerializer()
    {
        if (ProviderSettings.EnableMessageSerialization)
        {
            return base.GetSerializer();
        }
        // No serialization
        return new NullMessageSerializer();
    }

    protected override void BuildPendingRequestStore()
    {
        // Do not built it. Memory bus does not need it.
    }

    public override IDictionary<string, object> CreateHeaders()
    {
        if (ProviderSettings.EnableMessageSerialization)
        {
            return base.CreateHeaders();
        }
        // Memory bus does not require headers
        return null;
    }

    // Memory bus does not require requestId
    protected override string GenerateRequestId() => null;

    public override bool IsMessageScopeEnabled(ConsumerSettings consumerSettings, IDictionary<string, object> consumerContextProperties)
    {
#if NETSTANDARD2_0
        if (consumerSettings is null) throw new ArgumentNullException(nameof(consumerSettings));
#else
        ArgumentNullException.ThrowIfNull(consumerSettings);
#endif
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
                x => CreateMessageProcessor(x.OrderBy(consumerSettings => ConsumerModeOrder(consumerSettings)).ToList(), x.Key));

        _messageProcessorQueueByPath = ProviderSettings.EnableBlockingPublish
            ? []
            : _messageProcessorByPath.ToDictionary(x => x.Key, x => CreateMessageProcessorQueue(x.Value));
    }

    private IMessageProcessor<object> CreateMessageProcessor(IEnumerable<ConsumerSettings> consumerSettings, string path)
        => new MessageProcessor<object>(
            consumerSettings,
            this,
            path: path,
            responseProducer: null,
            messageProvider: ProviderSettings.EnableMessageSerialization
                ? (messageType, transportMessage) => Serializer.Deserialize(messageType, (byte[])transportMessage)
                : (messageType, transportMessage) => transportMessage,
            messageTypeProvider: ProviderSettings.EnableMessageSerialization
                ? null
                : transportMessage => transportMessage.GetType(),
            consumerErrorHandlerOpenGenericType: typeof(IMemoryConsumerErrorHandler<>));

    private IMessageProcessorQueue CreateMessageProcessorQueue(IMessageProcessor<object> messageProcessor)
    {
        var concurrency = messageProcessor.ConsumerSettings.Select(x => x.Instances).Max();
        return concurrency > 1
            ? new ConcurrentMessageProcessorQueue(messageProcessor, LoggerFactory.CreateLogger<ConcurrentMessageProcessorQueue>(), concurrency, CancellationToken)
            : new MessageProcessorQueue(messageProcessor, LoggerFactory.CreateLogger<MessageProcessorQueue>(), CancellationToken);
    }

    protected override Task ProduceToTransport(object message, string path, byte[] messagePayload, IDictionary<string, object> messageHeaders = null, CancellationToken cancellationToken = default)
        => Task.CompletedTask; // Not used

    public override Task ProduceResponse(string requestId, object request, IReadOnlyDictionary<string, object> requestHeaders, object response, Exception responseException, IMessageTypeConsumerInvokerSettings consumerInvoker)
        => Task.CompletedTask; // Not used to responses

    protected override Task PublishInternal(object message, string path, IDictionary<string, object> messageHeaders, CancellationToken cancellationToken, ProducerSettings producerSettings, IServiceProvider currentServiceProvider)
        => ProduceInternal<object>(message, path, messageHeaders, currentServiceProvider, isPublish: true, cancellationToken);

    protected override Task<TResponseMessage> SendInternal<TResponseMessage>(object request, string path, Type requestType, Type responseType, ProducerSettings producerSettings, DateTimeOffset created, DateTimeOffset expires, string requestId, IDictionary<string, object> requestHeaders, IServiceProvider currentServiceProvider, CancellationToken cancellationToken)
        => ProduceInternal<TResponseMessage>(request, path, requestHeaders, currentServiceProvider, isPublish: false, cancellationToken);

    #endregion

    private async Task<TResponseMessage> ProduceInternal<TResponseMessage>(object message, string path, IDictionary<string, object> requestHeaders, IServiceProvider currentServiceProvider, bool isPublish, CancellationToken cancellationToken)
    {
        var messageType = message.GetType();
        var producerSettings = GetProducerSettings(messageType);
        path ??= GetDefaultPath(producerSettings.MessageType, producerSettings);
        if (!_messageProcessorByPath.TryGetValue(path, out var messageProcessor))
        {
            _logger.LogDebug("No consumers interested in message type {MessageType} on path {Path}", messageType, path);
            return default;
        }

        var transportMessage = ProviderSettings.EnableMessageSerialization
            ? Serializer.Serialize(producerSettings.MessageType, message)
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

        // Execute the message processor in synchronous manner
        var r = await messageProcessor.ProcessMessage(transportMessage, messageHeadersReadOnly, currentServiceProvider: currentServiceProvider, cancellationToken: cancellationToken);
        if (r.Exception != null)
        {
            // We want to pass the same exception to the sender as it happened in the handler/consumer
            ExceptionDispatchInfo.Capture(r.Exception).Throw();
        }
        return (TResponseMessage)r.Response;
    }
}
