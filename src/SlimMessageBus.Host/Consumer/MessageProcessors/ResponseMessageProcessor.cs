namespace SlimMessageBus.Host;

/// <summary>
/// The <see cref="IMessageProcessor{TMessage}"/> implementation that processes the responses arriving to the bus.
/// </summary>
/// <typeparam name="TMessage"></typeparam>
public class ResponseMessageProcessor<TMessage> : IMessageProcessor<TMessage>
{
    private readonly ILogger<ResponseMessageProcessor<TMessage>> _logger;
    private readonly RequestResponseSettings _requestResponseSettings;
    private readonly IResponseConsumer _responseConsumer;
    private readonly IReadOnlyCollection<AbstractConsumerSettings> _consumerSettings;
    private readonly MessagePayloadProvider<TMessage> _messagePayloadProvider;

    public ResponseMessageProcessor(ILoggerFactory loggerFactory, RequestResponseSettings requestResponseSettings, IResponseConsumer responseConsumer, MessagePayloadProvider<TMessage> messagePayloadProvider)
    {
        if (loggerFactory is null) throw new ArgumentNullException(nameof(loggerFactory));

        _logger = loggerFactory.CreateLogger<ResponseMessageProcessor<TMessage>>();
        _requestResponseSettings = requestResponseSettings ?? throw new ArgumentNullException(nameof(requestResponseSettings));
        _responseConsumer = responseConsumer ?? throw new ArgumentNullException(nameof(responseConsumer));
        _consumerSettings = [_requestResponseSettings];
        _messagePayloadProvider = messagePayloadProvider ?? throw new ArgumentNullException(nameof(messagePayloadProvider));
    }

    public IReadOnlyCollection<AbstractConsumerSettings> ConsumerSettings => _consumerSettings;

    public async Task<ProcessMessageResult> ProcessMessage(TMessage transportMessage, IReadOnlyDictionary<string, object> messageHeaders, IDictionary<string, object> consumerContextProperties = null, IServiceProvider currentServiceProvider = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var messagePayload = _messagePayloadProvider(transportMessage);
            var exception = await _responseConsumer.OnResponseArrived(messagePayload, _requestResponseSettings.Path, messageHeaders);
            return new(exception, _requestResponseSettings, null);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error occurred while consuming response message, {Message}", transportMessage);

            // We can only continue and process all messages in the lease    
            return new(e, _requestResponseSettings, null);
        }
    }
}
