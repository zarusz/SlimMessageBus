namespace SlimMessageBus.Host.Memory;

using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.Serialization;
using System;

/// <summary>
/// In-memory message bus <see cref="IMessageBus"/> implementation to use for in process message passing.
/// </summary>
public class MemoryMessageBus : MessageBusBase
{
    private readonly ILogger _logger;
    private IDictionary<string, IMessageProcessor<object>> _consumersByPath;
    private readonly IMessageSerializer _serializer;
    private readonly MemoryMessageBusSettings _providerSettings;

    public override IMessageSerializer Serializer => _serializer;

    public MemoryMessageBus(MessageBusSettings settings, MemoryMessageBusSettings providerSettings) : base(settings)
    {
        _logger = LoggerFactory.CreateLogger<MemoryMessageBus>();
        _providerSettings = providerSettings ?? throw new ArgumentNullException(nameof(providerSettings));

        OnBuildProvider();

        _serializer = _providerSettings.EnableMessageSerialization
            ? Settings.Serializer
            : new NullMessageSerializer();
    }

    #region Overrides of MessageBusBase

    protected override void AssertSerializerSettings()
    {
        if (_providerSettings.EnableMessageSerialization)
        {
            base.AssertSerializerSettings();
        }
    }

    protected override void BuildPendingRequestStore()
    {
        // Do not built it. Memory bus does not need it.
    }

    public override bool IsMessageScopeEnabled(ConsumerSettings consumerSettings)
        => (consumerSettings ?? throw new ArgumentNullException(nameof(consumerSettings))).IsMessageScopeEnabled ?? Settings.IsMessageScopeEnabled ?? false; // by default Memory Bus has scoped message disabled

    protected override void Build()
    {
        base.Build();

        static int ConsumerModeOrder(ConsumerSettings settings) => settings.ConsumerMode == ConsumerMode.Consumer ? 0 : 1;

        _consumersByPath = Settings.Consumers
            .GroupBy(x => x.Path)
            .ToDictionary(
                x => x.Key,
                // Note: The consumers will first have IConsumer<>, then IRequestHandler<>
                x => CreateMessageProcessor(x.OrderBy(consumerSettings => ConsumerModeOrder(consumerSettings)).ToList(), x.Key));
    }

    private IMessageProcessor<object> CreateMessageProcessor(IEnumerable<ConsumerSettings> consumerSettings, string path)
        => new ConsumerInstanceMessageProcessor<object>(consumerSettings, this, MessageProvider, path: path,
            sendResponses: false,
            messageTypeProvider: _providerSettings.EnableMessageSerialization
                ? null
                : transportMessage => transportMessage.GetType());

    private object MessageProvider(Type messageType, object transportMessage)
    {
        if (_providerSettings.EnableMessageSerialization)
        {
            // The serialization is enabled, hence we need to deserialize bytes
            return Serializer.Deserialize(messageType, (byte[])transportMessage);
        }
        // The copy of the message is passed (searialization does not happen)
        return transportMessage;
    }

    public override IDictionary<string, object> CreateHeaders()
    {
        if (_providerSettings.EnableMessageSerialization)
        {
            return base.CreateHeaders();
        }
        // Memory bus does not require headers
        return null;
    }

    public override Task ProduceToTransport(object message, string path, byte[] messagePayload, IDictionary<string, object> messageHeaders = null, CancellationToken cancellationToken = default)
        => Task.CompletedTask; // Not used

    protected override Task PublishInternal(object message, string path, IDictionary<string, object> messageHeaders, CancellationToken cancellationToken, ProducerSettings producerSettings)
        => ProduceInternal<object>(message, path, producerSettings, messageHeaders, expectsResponse: false, cancellationToken);

    protected override Task<TResponseMessage> SendInternal<TResponseMessage>(object request, string path, Type requestType, Type responseType, ProducerSettings producerSettings, DateTimeOffset created, DateTimeOffset expires, string requestId, IDictionary<string, object> requestHeaders, CancellationToken cancellationToken)
        => ProduceInternal<TResponseMessage>(request, path, producerSettings, requestHeaders, expectsResponse: true, cancellationToken);

    #endregion

    private async Task<TResponseMessage> ProduceInternal<TResponseMessage>(object message, string path, ProducerSettings producerSettings, IDictionary<string, object> requestHeaders, bool expectsResponse, CancellationToken cancellationToken)
    {
        var messageType = message.GetType();
        if (!_consumersByPath.TryGetValue(path, out var messageProcessor))
        {
            _logger.LogDebug("No consumers interested in message type {MessageType} on path {Path}", messageType, path);
            return default;
        }

        Exception exception;
        object response = null;

        try
        {
            var transportMessage = _providerSettings.EnableMessageSerialization
                ? Serializer.Serialize(producerSettings.MessageType, message)
                : message;

            var messageHeadersReadOnly = requestHeaders != null
                ? requestHeaders as IReadOnlyDictionary<string, object> ?? new Dictionary<string, object>(requestHeaders)
                : null;

            (exception, var exceptionConsumerSettings, response) = await messageProcessor.ProcessMessage(transportMessage, messageHeadersReadOnly, cancellationToken);
            if (exception != null)
            {
                OnMessageFailed(message, exceptionConsumerSettings, exception);
            }
        }
        catch (Exception e)
        {
            exception = e;
        }

        if (exception != null)
        {
            throw exception;
        }

        if (response != null && response is TResponseMessage typedResponse)
        {
            return typedResponse;
        }

        return default;
    }

    private void OnMessageFailed(object message, AbstractConsumerSettings consumerSettings, Exception e)
    {
        try
        {
            _logger.LogDebug(e, "Error occured while executing {ConsumerType} on {Message} of type {MessageType}", consumerSettings is ConsumerSettings cs ? cs.ConsumerType : null, message, message?.GetType());
            // Invoke fault handler.
            consumerSettings.OnMessageFault?.Invoke(this, consumerSettings, message, e, null);
            Settings.OnMessageFault?.Invoke(this, consumerSettings, message, e, null);
        }
        catch (Exception hookEx)
        {
            HookFailed(_logger, hookEx, nameof(consumerSettings.OnMessageFault));
        }
    }
}
