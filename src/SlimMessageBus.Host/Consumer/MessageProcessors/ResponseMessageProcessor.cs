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
        if (requestResponseSettings is null) throw new ArgumentNullException(nameof(requestResponseSettings));
        if (responseConsumer is null) throw new ArgumentNullException(nameof(responseConsumer));
        if (messagePayloadProvider is null) throw new ArgumentNullException(nameof(messagePayloadProvider));

        _logger = loggerFactory.CreateLogger<ResponseMessageProcessor<TMessage>>();
        _requestResponseSettings = requestResponseSettings ?? throw new ArgumentNullException(nameof(requestResponseSettings));
        _responseConsumer = responseConsumer;
        _consumerSettings = new List<AbstractConsumerSettings> { _requestResponseSettings };
        _messagePayloadProvider = messagePayloadProvider ?? throw new ArgumentNullException(nameof(messagePayloadProvider));
    }

    public IReadOnlyCollection<AbstractConsumerSettings> ConsumerSettings => _consumerSettings;

    public async Task<(Exception Exception, AbstractConsumerSettings ConsumerSettings, object Response, object Message)> ProcessMessage(TMessage transportMessage, IReadOnlyDictionary<string, object> messageHeaders, CancellationToken cancellationToken, IServiceProvider currentServiceProvider = null)
    {
        try
        {
            var messagePayload = _messagePayloadProvider(transportMessage);
            var exception = await _responseConsumer.OnResponseArrived(messagePayload, _requestResponseSettings.Path, messageHeaders);
            return (exception, _requestResponseSettings, null, transportMessage);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error occured while consuming response message, {Message}", transportMessage);

            // We can only continue and process all messages in the lease    
            return (e, _requestResponseSettings, null, transportMessage);
        }
    }
}
