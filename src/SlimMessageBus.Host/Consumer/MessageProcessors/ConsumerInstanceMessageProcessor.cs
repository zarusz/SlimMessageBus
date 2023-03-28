namespace SlimMessageBus.Host;
/// <summary>
/// Implementation of <see cref="IMessageProcessor{TMessage}"/> that peforms orchestration around processing of a new message using an instance of the declared consumer (<see cref="IConsumer{TMessage}"/> or <see cref="IRequestHandler{TRequest, TResponse}"/> interface).
/// </summary>
/// <typeparam name="TTransportMessage"></typeparam>
public class ConsumerInstanceMessageProcessor<TTransportMessage> : MessageHandler, IMessageProcessor<TTransportMessage>
{
    private readonly ILogger _logger;
    private readonly Func<Type, TTransportMessage, object> _messageProvider;
    private readonly Func<TTransportMessage, Type> _messageTypeProvider;
    private readonly bool _sendResponses;
    private readonly bool _shouldFailWhenUnrecognizedMessageType;
    private readonly bool _shouldLogWhenUnrecognizedMessageType;

    protected IReadOnlyCollection<AbstractConsumerSettings> _consumerSettings;
    protected IReadOnlyCollection<IMessageTypeConsumerInvokerSettings> _invokers;
    protected IMessageTypeConsumerInvokerSettings _singleInvoker;

    public IReadOnlyCollection<AbstractConsumerSettings> ConsumerSettings => _consumerSettings;

    public ConsumerInstanceMessageProcessor(
        IEnumerable<AbstractConsumerSettings> consumerSettings,
        MessageBusBase messageBus,
        Func<Type, TTransportMessage, object> messageProvider,
        string path,
        Action<TTransportMessage, ConsumerContext> consumerContextInitializer = null,
        bool sendResponses = true,
        Func<TTransportMessage, Type> messageTypeProvider = null)
    : base(
        messageBus ?? throw new ArgumentNullException(nameof(messageBus)),
        messageScopeFactory: messageBus,
        messageTypeResolver: messageBus.MessageTypeResolver,
        messageHeadersFactory: messageBus,
        runtimeTypeCache: messageBus.RuntimeTypeCache,
        currentTimeProvider: messageBus,
        path: path,
        consumerContextInitializer == null ? null : (msg, ctx) => consumerContextInitializer((TTransportMessage)msg, ctx))
    {
        _logger = messageBus.LoggerFactory.CreateLogger<ConsumerInstanceMessageProcessor<TTransportMessage>>();
        _messageProvider = messageProvider ?? throw new ArgumentNullException(nameof(messageProvider));
        _messageTypeProvider = messageTypeProvider;
        _sendResponses = sendResponses;
        _consumerSettings = (consumerSettings ?? throw new ArgumentNullException(nameof(consumerSettings))).ToList();

        _invokers = consumerSettings.OfType<ConsumerSettings>().SelectMany(x => x.Invokers).ToList();
        _singleInvoker = _invokers.Count == 1 ? _invokers.First() : null;

        _shouldFailWhenUnrecognizedMessageType = consumerSettings.OfType<ConsumerSettings>().Any(x => x.UndeclaredMessageType.Fail);
        _shouldLogWhenUnrecognizedMessageType = consumerSettings.OfType<ConsumerSettings>().Any(x => x.UndeclaredMessageType.Log);
    }

    #region IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    protected virtual ValueTask DisposeAsyncCore() => new();

    #endregion

    public virtual async Task<(Exception Exception, AbstractConsumerSettings ConsumerSettings, object Response)> ProcessMessage(TTransportMessage transportMessage, IReadOnlyDictionary<string, object> messageHeaders, CancellationToken cancellationToken)
    {
        IMessageTypeConsumerInvokerSettings lastConsumerInvoker = null;
        Exception lastException = null;
        object lastResponse = null;

        try
        {
            var messageType = _messageTypeProvider != null
                ? _messageTypeProvider(transportMessage)
                : GetMessageType(messageHeaders);

            if (messageType != null)
            {
                try
                {
                    var message = _messageProvider(messageType, transportMessage);

                    var consumerInvokers = TryMatchConsumerInvoker(messageType);

                    foreach (var consumerInvoker in consumerInvokers)
                    {
                        lastConsumerInvoker = consumerInvoker;

                        // Skip the loop if it was cancelled
                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        (lastResponse, lastException, var requestId) = await DoHandle(message, messageHeaders, consumerInvoker, cancellationToken, transportMessage).ConfigureAwait(false);

                        if (consumerInvoker.ParentSettings.ConsumerMode == ConsumerMode.RequestResponse && _sendResponses)
                        {
                            await ProduceResponse(requestId, message, messageHeaders, lastResponse, lastException, consumerInvoker).ConfigureAwait(false);
                            lastResponse = null;
                            lastException = null;
                        }
                        else if (lastException != null)
                        {
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.LogDebug(e, "Processing of the message {TransportMessage} of type {MessageType} failed", transportMessage, messageType);
                    lastException = e;
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogDebug(e, "Processing of the message {TransportMessage} failed", transportMessage);
        }
        return (lastException, lastException != null ? lastConsumerInvoker?.ParentSettings : null, lastResponse);
    }

    private async Task ProduceResponse(string requestId, object request, IReadOnlyDictionary<string, object> requestHeaders, object response, Exception responseException, IMessageTypeConsumerInvokerSettings consumerInvoker)
    {
        // send the response (or error response)
        _logger.LogDebug("Serializing the response {Response} of type {MessageType} for RequestId: {RequestId}...", response, consumerInvoker.ParentSettings.ResponseType, requestId);

        var responseHeaders = MessageHeadersFactory.CreateHeaders();
        responseHeaders.SetHeader(ReqRespMessageHeaders.RequestId, requestId);
        if (responseException != null)
        {
            responseHeaders.SetHeader(ReqRespMessageHeaders.Error, responseException.Message);
        }
        await MessageBus.ProduceResponse(request, requestHeaders, response, responseHeaders, consumerInvoker.ParentSettings).ConfigureAwait(false);
    }

    protected Type GetMessageType(IReadOnlyDictionary<string, object> headers)
    {
        if (headers != null && headers.TryGetValue(MessageHeaders.MessageType, out var messageTypeValue) && messageTypeValue is string messageTypeName)
        {
            var messageType = MessageTypeResolver.ToType(messageTypeName);
            if (messageType != null)
            {
                _logger.LogDebug("Message type {MessageType} was declared in the message header", messageType);
                return messageType;
            }

            if (_shouldFailWhenUnrecognizedMessageType)
            {
                throw new MessageBusException($"The message arrived with {MessageHeaders.MessageType} header on path {Path}, but the type value {messageTypeName} is not a known type");
            }
            return null;
        }

        if (_singleInvoker != null)
        {
            _logger.LogDebug("No message type header was present, defaulting to the only declared message type {MessageType}", _singleInvoker.MessageType);
            return _singleInvoker.MessageType;
        }

        _logger.LogDebug("No message type header was present in the message header, multiple consumer types declared therefore cannot infer the message type");

        if (_shouldFailWhenUnrecognizedMessageType)
        {
            throw new MessageBusException($"The message arrived without {MessageHeaders.MessageType} header on path {Path}, so it is imposible to match one of the known consumer types {string.Join(",", _invokers.Select(x => x.ConsumerType.Name))}");
        }

        return null;
    }

    protected IEnumerable<IMessageTypeConsumerInvokerSettings> TryMatchConsumerInvoker(Type messageType)
    {
        if (_singleInvoker != null)
        {
            // fallback to the first one
            yield return _singleInvoker;
        }
        else
        {
            var found = false;
            foreach (var invoker in _invokers)
            {
                if (MessageBus.RuntimeTypeCache.IsAssignableFrom(messageType, invoker.MessageType))
                {
                    found = true;
                    yield return invoker;
                }
            }

            if (!found)
            {
                if (_shouldLogWhenUnrecognizedMessageType)
                {
                    _logger.LogInformation("The message on path {Path} declared {HeaderName} header of type {MessageType}, but none of the the known consumer types {ConsumerTypes} was able to handle that", Path, MessageHeaders.MessageType, messageType, string.Join(",", _invokers.Select(x => x.ConsumerType.Name)));
                }

                if (_shouldFailWhenUnrecognizedMessageType)
                {
                    throw new MessageBusException($"The message on path {Path} declared {MessageHeaders.MessageType} header of type {messageType}, but none of the the known consumer types {string.Join(",", _invokers.Select(x => x.ConsumerType.Name))} was able to handle that");
                }
            }
        }
    }
}