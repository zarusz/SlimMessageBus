namespace SlimMessageBus.Host.Memory;

using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.Serialization;

/// <summary>
/// In-memory message bus <see cref="IMessageBus"/> implementation to use for in process message passing.
/// </summary>
public class MemoryMessageBus : MessageBusBase
{
    private readonly ILogger _logger;
    private IDictionary<string, List<MessageHandler>> _consumersByPath;
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
                x => x.OrderBy(consumerSettings => ConsumerModeOrder(consumerSettings))
                      .Select(consumerSettings => new MessageHandler(consumerSettings, this))
                      .ToList());
    }

    public override IDictionary<string, object> CreateHeaders() => null; // Memory bus requires no headers

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
        if (!_consumersByPath.TryGetValue(path, out var consumers) || consumers.Count == 0)
        {
            _logger.LogDebug("No consumers interested in message type {MessageType} on path {Path}", messageType, path);
            return default;
        }

        var messagePayload = Serializer.Serialize(producerSettings.MessageType, message);
        var messageHeadersReadOnly = requestHeaders != null
            ? requestHeaders as IReadOnlyDictionary<string, object> ?? new Dictionary<string, object>(requestHeaders)
            : null;

        // Note: The consumers will first have IConsumer<>, then IRequestHandler<>
        foreach (var consumer in consumers)
        {
            _logger.LogDebug("Executing consumer {ConsumerType} on {Message}...", consumer.ConsumerSettings.ConsumerType, message);
            var response = await OnMessageProduced(messageType, message, messagePayload, messageHeadersReadOnly, consumer, cancellationToken);
            _logger.LogTrace("Executed consumer {ConsumerType}", consumer.ConsumerSettings.ConsumerType);

            if (expectsResponse && consumer.ConsumerSettings.ConsumerMode == ConsumerMode.RequestResponse && response is TResponseMessage typedResponse)
            {
                // Return the first response from the Handler that matches
                return typedResponse;
            }
        }

        return default;
    }

    private async Task<object> OnMessageProduced(Type messageType, object message, byte[] messagePayload, IReadOnlyDictionary<string, object> messageHeaders, MessageHandler consumer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Exception responseException;
        object response;
        try
        {
            // Note: Will pass a deep copy of the message (if serialization enabled) or the original message if serialization not enabled
            var messageForConsumer = Serializer.Deserialize(messageType, messagePayload) ?? message;

            (response, responseException, _) = await consumer.DoHandle(message: messageForConsumer, messageHeaders: messageHeaders, nativeMessage: null).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            OnMessageFailed(message, consumer, e);
            throw;
        }

        if (responseException != null)
        {
            OnMessageFailed(message, consumer, responseException);
            throw responseException;
        }

        if (consumer.ConsumerSettings.ConsumerMode == ConsumerMode.RequestResponse)
        {
            // will be null when serialization is not enabled
            return response;
        }
        return null;
    }

    private void OnMessageFailed(object message, MessageHandler consumer, Exception e)
    {
        try
        {
            _logger.LogDebug(e, "Error occured while executing {ConsumerType} on {Message}", consumer.ConsumerSettings.ConsumerType, message);
            // Invoke fault handler.
            consumer.ConsumerSettings.OnMessageFault?.Invoke(this, consumer.ConsumerSettings, message, e, null);
            Settings.OnMessageFault?.Invoke(this, consumer.ConsumerSettings, message, e, null);
        }
        catch (Exception hookEx)
        {
            HookFailed(_logger, hookEx, nameof(consumer.ConsumerSettings.OnMessageFault));
        }
    }
}
