namespace SlimMessageBus.Host;

/// <summary>
/// Implementation of <see cref="IMessageProcessor{TMessage}"/> that peforms orchestration around processing of a new message using an instance of the declared consumer (<see cref="IConsumer{TMessage}"/> or <see cref="IRequestHandler{TRequest, TResponse}"/> interface).
/// </summary>
/// <typeparam name="TTransportMessage"></typeparam>
public class MessageProcessor<TTransportMessage> : MessageHandler, IMessageProcessor<TTransportMessage>
{
    private readonly ILogger _logger;
    private readonly MessageProvider<TTransportMessage> _messageProvider;
    private readonly MessageTypeProvider<TTransportMessage> _messageTypeProvider;
    private readonly bool _shouldFailWhenUnrecognizedMessageType;
    private readonly bool _shouldLogWhenUnrecognizedMessageType;
    private readonly IResponseProducer _responseProducer;
    private readonly ConsumerContextInitializer<TTransportMessage> _consumerContextInitializer;

    protected IReadOnlyCollection<AbstractConsumerSettings> _consumerSettings;
    protected IReadOnlyCollection<IMessageTypeConsumerInvokerSettings> _invokers;
    protected IMessageTypeConsumerInvokerSettings _singleInvoker;

    public IReadOnlyCollection<AbstractConsumerSettings> ConsumerSettings => _consumerSettings;

    public MessageProcessor(
        IEnumerable<AbstractConsumerSettings> consumerSettings,
        MessageBusBase messageBus,
        MessageProvider<TTransportMessage> messageProvider,
        string path,
        IResponseProducer responseProducer,
        ConsumerContextInitializer<TTransportMessage> consumerContextInitializer = null,
        MessageTypeProvider<TTransportMessage> messageTypeProvider = null,
        Type consumerErrorHandlerOpenGenericType = null)
    : base(
        messageBus ?? throw new ArgumentNullException(nameof(messageBus)),
        messageScopeFactory: messageBus,
        messageTypeResolver: messageBus.MessageTypeResolver,
        messageHeadersFactory: messageBus,
        runtimeTypeCache: messageBus.RuntimeTypeCache,
        currentTimeProvider: messageBus,
        path: path,
        consumerErrorHandlerOpenGenericType)
    {
        _logger = messageBus.LoggerFactory.CreateLogger<MessageProcessor<TTransportMessage>>();
        _messageProvider = messageProvider ?? throw new ArgumentNullException(nameof(messageProvider));
        _messageTypeProvider = messageTypeProvider;
        _consumerSettings = (consumerSettings ?? throw new ArgumentNullException(nameof(consumerSettings))).ToList();
        _responseProducer = responseProducer;

        _consumerContextInitializer = consumerContextInitializer;

        _invokers = consumerSettings.OfType<ConsumerSettings>().SelectMany(x => x.Invokers).ToList();
        _singleInvoker = _invokers.Count == 1 ? _invokers.First() : null;

        _shouldFailWhenUnrecognizedMessageType = consumerSettings.OfType<ConsumerSettings>().Any(x => x.UndeclaredMessageType.Fail);
        _shouldLogWhenUnrecognizedMessageType = consumerSettings.OfType<ConsumerSettings>().Any(x => x.UndeclaredMessageType.Log);
    }

    protected override ConsumerContext CreateConsumerContext(IReadOnlyDictionary<string, object> messageHeaders, IMessageTypeConsumerInvokerSettings consumerInvoker, object transportMessage, object consumerInstance, IMessageBus messageBus, IDictionary<string, object> consumerContextProperties, CancellationToken cancellationToken)
    {
        var context = base.CreateConsumerContext(messageHeaders, consumerInvoker, transportMessage, consumerInstance, messageBus, consumerContextProperties, cancellationToken);

        _consumerContextInitializer?.Invoke((TTransportMessage)transportMessage, context);

        return context;
    }

    public async virtual Task<ProcessMessageResult> ProcessMessage(TTransportMessage transportMessage, IReadOnlyDictionary<string, object> messageHeaders, IDictionary<string, object> consumerContextProperties = null, IServiceProvider currentServiceProvider = null, CancellationToken cancellationToken = default)
    {
        IMessageTypeConsumerInvokerSettings lastConsumerInvoker = null;
        Exception lastException = null;
        object lastResponse = null;
        object message = null;

        try
        {
            var messageType = _messageTypeProvider != null
                ? _messageTypeProvider(transportMessage)
                : GetMessageType(messageHeaders);

            if (messageType != null)
            {
                try
                {
                    message = _messageProvider(messageType, transportMessage);

                    var consumerInvokers = TryMatchConsumerInvoker(messageType);

                    foreach (var consumerInvoker in consumerInvokers)
                    {
                        lastConsumerInvoker = consumerInvoker;

                        // Skip the loop if it was cancelled
                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        (lastResponse, lastException, var requestId) = await DoHandle(message, messageHeaders, consumerInvoker, transportMessage, consumerContextProperties, currentServiceProvider, cancellationToken).ConfigureAwait(false);

                        if (consumerInvoker.ParentSettings.ConsumerMode == ConsumerMode.RequestResponse && _responseProducer != null)
                        {
                            if (!ReferenceEquals(ResponseForExpiredRequest, lastResponse))
                            {
                                // We discard expired requests, so there is no reponse to provide
                                await _responseProducer.ProduceResponse(requestId, message, messageHeaders, lastResponse, lastException, consumerInvoker).ConfigureAwait(false);
                            }
                            // Clear the exception as it will be returned to the sender.
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
                    lastException ??= e;
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogDebug(e, "Processing of the message {TransportMessage} failed", transportMessage);
            lastException = e;
        }
        return new(lastException, lastException != null ? lastConsumerInvoker?.ParentSettings : null, lastResponse, message);
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
                if (RuntimeTypeCache.IsAssignableFrom(messageType, invoker.MessageType))
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
                    throw new ConsumerMessageBusException($"The message on path {Path} declared {MessageHeaders.MessageType} header of type {messageType}, but none of the the known consumer types {string.Join(",", _invokers.Select(x => x.ConsumerType.Name))} was able to handle that");
                }
            }
        }
    }
}