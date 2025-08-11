namespace SlimMessageBus.Host;

using System.Diagnostics;

using SlimMessageBus.Host.Consumer;

/// <summary>
/// Implementation of <see cref="IMessageProcessor{TMessage}"/> that performs orchestration around processing of a new message using an instance of the declared consumer (<see cref="IConsumer{TMessage}"/> or <see cref="IRequestHandler{TRequest, TResponse}"/> interface).
/// </summary>
/// <typeparam name="TTransportMessage"></typeparam>
public partial class MessageProcessor<TTransportMessage> : MessageHandler, IMessageProcessor<TTransportMessage>
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
        timeProvider: messageBus.TimeProvider,
        path: path,
        consumerErrorHandlerOpenGenericType)
    {
        _logger = messageBus.LoggerFactory.CreateLogger<MessageProcessor<TTransportMessage>>();
        _messageProvider = messageProvider ?? throw new ArgumentNullException(nameof(messageProvider));
        _messageTypeProvider = messageTypeProvider;
        _consumerSettings = [.. consumerSettings ?? throw new ArgumentNullException(nameof(consumerSettings))];
        _responseProducer = responseProducer;

        _consumerContextInitializer = consumerContextInitializer;

        _invokers = [.. consumerSettings.OfType<ConsumerSettings>().SelectMany(x => x.Invokers)];
        _singleInvoker = _invokers.Count == 1 ? _invokers.First() : null;

        // true by default, if not set on the consumer settings
        _shouldFailWhenUnrecognizedMessageType = !consumerSettings.Any(x => x.UndeclaredMessageType.Fail.HasValue)
            || consumerSettings.Any(x => x.UndeclaredMessageType.Fail.HasValue && x.UndeclaredMessageType.Fail.Value);
        // false by default, if not set on the consumer settings
        _shouldLogWhenUnrecognizedMessageType = consumerSettings.Any(x => x.UndeclaredMessageType.Log.HasValue)
            && consumerSettings.Any(x => x.UndeclaredMessageType.Log.HasValue && x.UndeclaredMessageType.Log.Value);
    }

    protected override ConsumerContext CreateConsumerContext(IMessageScope messageScope,
                                                             IReadOnlyDictionary<string, object> messageHeaders,
                                                             IMessageTypeConsumerInvokerSettings consumerInvoker,
                                                             object transportMessage,
                                                             object consumerInstance,
                                                             IMessageBus messageBus,
                                                             IDictionary<string, object> consumerContextProperties,
                                                             CancellationToken cancellationToken)
    {
        var context = base.CreateConsumerContext(messageScope, messageHeaders, consumerInvoker, transportMessage, consumerInstance, messageBus, consumerContextProperties, cancellationToken);

        _consumerContextInitializer?.Invoke((TTransportMessage)transportMessage, context);

        return context;
    }

    public async virtual Task<ProcessMessageResult> ProcessMessage(TTransportMessage transportMessage, IReadOnlyDictionary<string, object> messageHeaders, IDictionary<string, object> consumerContextProperties = null, IServiceProvider currentServiceProvider = null, CancellationToken cancellationToken = default)
    {
        IMessageTypeConsumerInvokerSettings lastConsumerInvoker = null;
        var result = ProcessResult.Success;
        Exception lastException = null;
        object lastResponse = null;
        Type messageType = null;

        try
        {
            messageType = _messageTypeProvider != null
                ? _messageTypeProvider(transportMessage, messageHeaders)
                : GetMessageType(messageHeaders);

            if (messageType != null)
            {
                var message = _messageProvider(messageType, messageHeaders, transportMessage);
                try
                {
                    var consumerInvokers = TryMatchConsumerInvoker(messageType);

                    foreach (var consumerInvoker in consumerInvokers)
                    {
                        lastConsumerInvoker = consumerInvoker;

                        // Skip the loop if it was cancelled
                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        (result, lastResponse, lastException, var requestId) = await DoHandle(message, messageHeaders, consumerInvoker, transportMessage, consumerContextProperties, currentServiceProvider, cancellationToken).ConfigureAwait(false);

                        Debug.Assert(result is not ProcessResult.RetryState);

                        if (consumerInvoker.ParentSettings.ConsumerMode == ConsumerMode.RequestResponse && _responseProducer != null)
                        {
                            if (!ReferenceEquals(ResponseForExpiredRequest, lastResponse))
                            {
                                // We discard expired requests, so there is no response to provide
                                await _responseProducer.ProduceResponse(requestId, message, messageHeaders, lastResponse, lastException, consumerInvoker, cancellationToken).ConfigureAwait(false);
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
                finally
                {
                    if (message is IAsyncDisposable asyncDisposable)
                    {
                        await asyncDisposable.DisposeAsync();
                    }
                    else if (message is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
            }
        }
        catch (Exception e)
        {
            LogProcessingMessageFailedTypeKnown(transportMessage, messageType, e);
            lastException ??= e;
            result = ProcessResult.Failure;
        }
        return new(result, lastException, lastException != null ? lastConsumerInvoker?.ParentSettings : null, lastResponse);
    }

    protected Type GetMessageType(IReadOnlyDictionary<string, object> headers)
    {
        if (headers != null && headers.TryGetValue(MessageHeaders.MessageType, out var messageTypeValue) && messageTypeValue is string messageTypeName)
        {
            var messageType = MessageTypeResolver.ToType(messageTypeName);
            if (messageType != null)
            {
                LogMessageTypeDeclaredInHeader(messageType);
                return messageType;
            }

            if (_shouldFailWhenUnrecognizedMessageType)
            {
                throw new ConsumerMessageBusException($"The message arrived with {MessageHeaders.MessageType} header on path {Path}, but the type value {messageTypeName} is not a known type");
            }
            return null;
        }

        if (_singleInvoker != null)
        {
            LogMessageTypeHeaderMissingAndDefaulting(_singleInvoker.MessageType);
            return _singleInvoker.MessageType;
        }

        LogNoMessageTypeHeaderPresent();

        if (_shouldFailWhenUnrecognizedMessageType)
        {
            throw new ConsumerMessageBusException($"The message arrived without {MessageHeaders.MessageType} header on path {Path}, so it is impossible to match one of the known consumer types {string.Join(",", _invokers.Select(x => x.ConsumerType.Name))}");
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
            foreach (var invoker in _invokers.Where(x => RuntimeTypeCache.IsAssignableFrom(messageType, x.MessageType)))
            {
                found = true;
                yield return invoker;
            }

            if (!found)
            {
                if (_shouldLogWhenUnrecognizedMessageType)
                {
                    var consumerTypes = string.Join(",", _invokers.Select(x => x.ConsumerType.Name));
                    LogNoConsumerTypeMatched(messageType, Path, MessageHeaders.MessageType, consumerTypes);
                }

                if (_shouldFailWhenUnrecognizedMessageType)
                {
                    throw new ConsumerMessageBusException($"The message on path {Path} declared {MessageHeaders.MessageType} header of type {messageType}, but none of the known consumer types {string.Join(",", _invokers.Select(x => x.ConsumerType.Name))} was able to handle that");
                }
            }
        }
    }

    #region Logging

    [LoggerMessage(
       EventId = 0,
       Level = LogLevel.Debug,
       Message = "Processing of the message {TransportMessage} of type {MessageType} failed")]
    private partial void LogProcessingMessageFailedTypeKnown(TTransportMessage transportMessage, Type messageType, Exception e);

    [LoggerMessage(
       EventId = 1,
       Level = LogLevel.Debug,
       Message = "Message type {MessageType} was declared in the message header")]
    private partial void LogMessageTypeDeclaredInHeader(Type messageType);

    [LoggerMessage(
       EventId = 2,
       Level = LogLevel.Debug,
       Message = "No message type header was present, defaulting to the only declared message type {MessageType}")]
    private partial void LogMessageTypeHeaderMissingAndDefaulting(Type messageType);

    [LoggerMessage(
       EventId = 3,
       Level = LogLevel.Debug,
       Message = "No message type header was present in the message header, multiple consumer types declared therefore cannot infer the message type")]
    private partial void LogNoMessageTypeHeaderPresent();

    [LoggerMessage(
       EventId = 4,
       Level = LogLevel.Information,
       Message = "The message on path {Path} declared {HeaderName} header of type {MessageType}, but none of the known consumer types {ConsumerTypes} was able to handle it")]
    private partial void LogNoConsumerTypeMatched(Type messageType, string path, string headerName, string consumerTypes);

    #endregion
}

#if NETSTANDARD2_0

public partial class MessageProcessor<TTransportMessage>
{
    private partial void LogProcessingMessageFailedTypeKnown(TTransportMessage transportMessage, Type messageType, Exception e)
        => _logger.LogDebug(e, "Processing of the message {TransportMessage} of type {MessageType} failed", transportMessage, messageType);

    private partial void LogMessageTypeDeclaredInHeader(Type messageType)
        => _logger.LogDebug("Message type {MessageType} was declared in the message header", messageType);

    private partial void LogMessageTypeHeaderMissingAndDefaulting(Type messageType)
        => _logger.LogDebug("No message type header was present, defaulting to the only declared message type {MessageType}", messageType);

    private partial void LogNoMessageTypeHeaderPresent()
        => _logger.LogDebug("No message type header was present in the message header, multiple consumer types declared therefore cannot infer the message type");

    private partial void LogNoConsumerTypeMatched(Type messageType, string path, string headerName, string consumerTypes)
        => _logger.LogInformation("The message on path {Path} declared {HeaderName} header of type {MessageType}, but none of the known consumer types {ConsumerTypes} was able to handle it", path, headerName, messageType, consumerTypes);
}

#endif
