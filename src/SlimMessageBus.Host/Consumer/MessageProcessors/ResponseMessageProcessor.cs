namespace SlimMessageBus.Host;

/// <summary>
/// The <see cref="IMessageProcessor{TMessage}"/> implementation that processes the responses arriving to the bus.
/// </summary>
/// <typeparam name="TMessage"></typeparam>
public class ResponseMessageProcessor<TMessage> : IMessageProcessor<TMessage>
{
    private readonly ILogger<ResponseMessageProcessor<TMessage>> _logger;
    private readonly RequestResponseSettings _requestResponseSettings;
    private readonly IReadOnlyCollection<AbstractConsumerSettings> _consumerSettings;
    private readonly MessageBusBase _messageBus;
    private readonly Func<TMessage, byte[]> _messageProvider;

    public ResponseMessageProcessor(RequestResponseSettings requestResponseSettings, MessageBusBase messageBus, Func<TMessage, byte[]> messageProvider)
    {
        if (messageBus is null) throw new ArgumentNullException(nameof(messageBus));

        _logger = messageBus.LoggerFactory.CreateLogger<ResponseMessageProcessor<TMessage>>();
        _requestResponseSettings = requestResponseSettings ?? throw new ArgumentNullException(nameof(requestResponseSettings));
        _consumerSettings = new List<AbstractConsumerSettings> { _requestResponseSettings };
        _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
        _messageProvider = messageProvider ?? throw new ArgumentNullException(nameof(messageProvider));
    }

    public IReadOnlyCollection<AbstractConsumerSettings> ConsumerSettings => _consumerSettings;

    [Obsolete]
    public async Task<Exception> ProcessMessage(TMessage message, IReadOnlyDictionary<string, object> messageHeaders, CancellationToken cancellationToken, IMessageTypeConsumerInvokerSettings consumerInvoker, IServiceProvider currentServiceProvider = null)
    {
        var (exception, _, _) = await ProcessMessage(message, messageHeaders, cancellationToken, currentServiceProvider);
        return exception;
    }

    public async Task<(Exception Exception, AbstractConsumerSettings ConsumerSettings, object Response)> ProcessMessage(TMessage message, IReadOnlyDictionary<string, object> messageHeaders, CancellationToken cancellationToken, IServiceProvider currentServiceProvider = null)
    {
        try
        {
            var messagePayload = _messageProvider(message);
            var exception = await _messageBus.OnResponseArrived(messagePayload, _requestResponseSettings.Path, messageHeaders);
            return (exception, _requestResponseSettings, null);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error occured while consuming response message, {Message}", message);

            if (_requestResponseSettings.OnResponseMessageFault != null)
            {
                // Call the hook
                try
                {
                    _requestResponseSettings.OnResponseMessageFault(_requestResponseSettings, message, e);
                }
                catch (Exception eh)
                {
                    MessageBusBase.HookFailed(_logger, eh, nameof(IConsumerEvents.OnMessageFault));
                }
            }

            // We can only continue and process all messages in the lease    
            return (e, _requestResponseSettings, null);
        }
    }

    #region IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    protected virtual ValueTask DisposeAsyncCore() => new();

    #endregion
}