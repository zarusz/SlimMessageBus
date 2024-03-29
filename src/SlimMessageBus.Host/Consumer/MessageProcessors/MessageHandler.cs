namespace SlimMessageBus.Host;

using SlimMessageBus.Host.Consumer;

public class MessageHandler : IMessageHandler
{
    private readonly ILogger _logger;
    private readonly IMessageScopeFactory _messageScopeFactory;
    private readonly ICurrentTimeProvider _currentTimeProvider;

    protected RuntimeTypeCache RuntimeTypeCache { get; }
    protected IMessageTypeResolver MessageTypeResolver { get; }
    protected IMessageHeadersFactory MessageHeadersFactory { get; }
    protected Type ConsumerErrorHandlerOpenGenericType { get; }

    public MessageBusBase MessageBus { get; }
    public string Path { get; }

    /// <summary>
    /// Represents a response that has been discarded (it expired)
    /// </summary>
    protected static readonly object ResponseForExpiredRequest = new();

    public MessageHandler(
        MessageBusBase messageBus,
        IMessageScopeFactory messageScopeFactory,
        IMessageTypeResolver messageTypeResolver,
        IMessageHeadersFactory messageHeadersFactory,
        RuntimeTypeCache runtimeTypeCache,
        ICurrentTimeProvider currentTimeProvider,
        string path,
        Type consumerErrorHandlerOpenGenericType = null)
    {
#if NETSTANDARD2_0
        if (messageBus is null) throw new ArgumentNullException(nameof(messageBus));
#else
        ArgumentNullException.ThrowIfNull(messageBus);
#endif

        _logger = messageBus.LoggerFactory.CreateLogger<MessageHandler>();
        _messageScopeFactory = messageScopeFactory;
        _currentTimeProvider = currentTimeProvider;

        RuntimeTypeCache = runtimeTypeCache;
        MessageTypeResolver = messageTypeResolver;
        MessageHeadersFactory = messageHeadersFactory;
        MessageBus = messageBus;
        Path = path ?? throw new ArgumentNullException(nameof(path));

        if (consumerErrorHandlerOpenGenericType is not null)
        {
            // Validate that the type is an open generic type of IConsumerErrorHandler<> (e.g. IMemoryConsumerErrorHandler<> which derives from IConsumerErrorHandler<>).
            if (!consumerErrorHandlerOpenGenericType.IsGenericTypeDefinition || !typeof(IConsumerErrorHandler<object>).IsAssignableFrom(consumerErrorHandlerOpenGenericType.MakeGenericType(typeof(object))))
            {
                throw new ArgumentException($"The type {consumerErrorHandlerOpenGenericType} needs to be an open generic type of {typeof(IConsumerErrorHandler<>)}", paramName: nameof(consumerErrorHandlerOpenGenericType));
            }
            ConsumerErrorHandlerOpenGenericType = consumerErrorHandlerOpenGenericType;
        }
    }

    public async Task<(object Response, Exception ResponseException, string RequestId)> DoHandle(object message, IReadOnlyDictionary<string, object> messageHeaders, IMessageTypeConsumerInvokerSettings consumerInvoker, object transportMessage = null, IDictionary<string, object> consumerContextProperties = null, IServiceProvider currentServiceProvider = null, CancellationToken cancellationToken = default)
    {
        var messageType = message.GetType();

        var hasResponse = consumerInvoker.ParentSettings.ConsumerMode == ConsumerMode.RequestResponse;
        var responseType = hasResponse ? consumerInvoker.ParentSettings.ResponseType ?? typeof(Void) : null;

        object response = null;
        Exception responseException = null;
        string requestId = null;

        if (hasResponse && messageHeaders != null)
        {
            messageHeaders.TryGetHeader(ReqRespMessageHeaders.RequestId, out requestId);
        }

        await using (var messageScope = _messageScopeFactory.CreateMessageScope(consumerInvoker.ParentSettings, message, consumerContextProperties, currentServiceProvider))
        {
            if (messageHeaders != null && messageHeaders.TryGetHeader(ReqRespMessageHeaders.Expires, out DateTimeOffset? expires) && expires != null)
            {
                // Verify if the request/message is already expired
                var currentTime = _currentTimeProvider.CurrentTime;
                if (currentTime > expires.Value)
                {
                    // ToDo: Call interceptor

                    // Do not process the expired message
                    return (ResponseForExpiredRequest, null, requestId);
                }
            }

            var messageBusTarget = new MessageBusProxy(MessageBus, messageScope.ServiceProvider);

            Type consumerType = null;
            object consumerInstance = null;

            try
            {
                consumerType = consumerInvoker.ConsumerType;
                consumerInstance = messageScope.ServiceProvider.GetService(consumerType)
                    ?? throw new ConfigurationMessageBusException($"Could not resolve consumer/handler type {consumerType} from the DI container. Please check that the configured type {consumerType} is registered within the DI container.");

                var consumerContext = CreateConsumerContext(messageHeaders, consumerInvoker, transportMessage, consumerInstance, messageBusTarget, consumerContextProperties, cancellationToken);
                try
                {
                    response = await DoHandleInternal(message, consumerInvoker, messageType, hasResponse, responseType, messageScope, consumerContext).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Give a chance to the consumer error handler to take action
                    var handleErrorResult = await DoHandleError(message, consumerInvoker, messageType, hasResponse, responseType, messageScope, consumerContext, ex).ConfigureAwait(false);
                    if (!handleErrorResult.Handled)
                    {
                        responseException = ex;
                    }
                    if (handleErrorResult.HasResponse)
                    {
                        response = handleErrorResult.Response;
                    }
                }
            }
            catch (Exception e)
            {
                responseException = e;
            }
            finally
            {
                if (consumerInvoker.ParentSettings.IsDisposeConsumerEnabled && consumerInstance is IDisposable consumerInstanceDisposable)
                {
                    _logger.LogDebug("Disposing consumer instance {Consumer} of type {ConsumerType}", consumerInstance, consumerType);
                    consumerInstanceDisposable.DisposeSilently("ConsumerInstance", _logger);
                }
            }
        }

        return (response, responseException, requestId);
    }

    private async Task<object> DoHandleInternal(object message, IMessageTypeConsumerInvokerSettings consumerInvoker, Type messageType, bool hasResponse, Type responseType, IMessageScope messageScope, IConsumerContext consumerContext)
    {
        var consumerInterceptors = RuntimeTypeCache.ConsumerInterceptorType.ResolveAll(messageScope.ServiceProvider, messageType);
        var handlerInterceptors = hasResponse ? RuntimeTypeCache.HandlerInterceptorType.ResolveAll(messageScope.ServiceProvider, (messageType, responseType)) : null;
        if (consumerInterceptors != null || handlerInterceptors != null)
        {
            var pipeline = new ConsumerInterceptorPipeline(RuntimeTypeCache, this, message, responseType, consumerContext, consumerInvoker, consumerInterceptors: consumerInterceptors, handlerInterceptors: handlerInterceptors);
            return await pipeline.Next().ConfigureAwait(false);
        }

        // call without interceptors
        return await ExecuteConsumer(message, consumerContext, consumerInvoker, responseType).ConfigureAwait(false);
    }

    private async Task<ConsumerErrorHandlerResult> DoHandleError(object message, IMessageTypeConsumerInvokerSettings consumerInvoker, Type messageType, bool hasResponse, Type responseType, IMessageScope messageScope, IConsumerContext consumerContext, Exception ex)
    {
        var errorHandlerResult = ConsumerErrorHandlerResult.Failure;

        // Use the bus provider specific error handler type first (if provided)
        var consumerErrorHandler = ConsumerErrorHandlerOpenGenericType is not null
            ? GetConsumerErrorHandler(messageType, ConsumerErrorHandlerOpenGenericType, messageScope.ServiceProvider)
            : null;

        // Use the default error handler type as the last resort
        consumerErrorHandler ??= GetConsumerErrorHandler(messageType, typeof(IConsumerErrorHandler<>), messageScope.ServiceProvider);

        if (consumerErrorHandler != null)
        {
            _logger.LogDebug(ex, "Consumer error handler of type {ConsumerErrorHandlerType} will be used to handle the exception during processing of message of type {MessageType}", consumerErrorHandler.GetType(), messageType);

            // Give a chance to the consumer error handler to take action
            Task<object> retry() => DoHandleInternal(message, consumerInvoker, messageType, hasResponse, responseType, messageScope, consumerContext);

            var consumerErrorHandlerMethod = RuntimeTypeCache.ConsumerErrorHandlerType[messageType];
            errorHandlerResult = await consumerErrorHandlerMethod(consumerErrorHandler, message, retry, consumerContext, ex).ConfigureAwait(false);
        }

        return errorHandlerResult;
    }

    private object GetConsumerErrorHandler(Type messageType, Type consumerErrorHandlerOpenGenericType, IServiceProvider messageScope)
    {
        var consumerErrorHandlerType = RuntimeTypeCache.GetClosedGenericType(consumerErrorHandlerOpenGenericType, messageType);
        return messageScope.GetService(consumerErrorHandlerType);
    }

    protected virtual ConsumerContext CreateConsumerContext(IReadOnlyDictionary<string, object> messageHeaders, IMessageTypeConsumerInvokerSettings consumerInvoker, object transportMessage, object consumerInstance, IMessageBus messageBus, IDictionary<string, object> consumerContextProperties, CancellationToken cancellationToken)
        => new(consumerContextProperties)
        {
            Path = Path,
            Headers = messageHeaders,
            Bus = messageBus,
            CancellationToken = cancellationToken,
            Consumer = consumerInstance,
            ConsumerInvoker = consumerInvoker
        };

    public async Task<object> ExecuteConsumer(object message, IConsumerContext consumerContext, IMessageTypeConsumerInvokerSettings consumerInvoker, Type responseType)
    {
        if (RuntimeTypeCache.IsAssignableFrom(consumerContext.Consumer.GetType(), typeof(IConsumerWithContext)))
        {
            var consumerWithContext = (IConsumerWithContext)consumerContext.Consumer;
            consumerWithContext.Context = consumerContext;
        }

        // the consumer just subscribes to the message
        var task = consumerInvoker.ConsumerMethod(consumerContext.Consumer, message, consumerContext, consumerContext.CancellationToken);
        await task.ConfigureAwait(false);

        if (responseType != null && responseType != typeof(Void))
        {
            // the consumer handles the request (and replies)
            var taskOfType = RuntimeTypeCache.GetTaskOfType(responseType);

            var response = taskOfType.GetResult(task);
            return response;
        }

        return null;
    }
}