namespace SlimMessageBus.Host;

using SlimMessageBus.Host.Collections;

public class MessageHandler : IMessageHandler
{
    private readonly ILogger _logger;
    private readonly IMessageScopeFactory _messageScopeFactory;
    protected RuntimeTypeCache RuntimeTypeCache { get; }
    private readonly ICurrentTimeProvider _currentTimeProvider;
    private readonly Action<object, ConsumerContext> _consumerContextInitializer;

    protected IMessageTypeResolver MessageTypeResolver { get; }
    protected IMessageHeadersFactory MessageHeadersFactory { get; }
    public MessageBusBase MessageBus { get; }
    public string Path { get; }

    public MessageHandler(MessageBusBase messageBus, IMessageScopeFactory messageScopeFactory, IMessageTypeResolver messageTypeResolver, IMessageHeadersFactory messageHeadersFactory, RuntimeTypeCache runtimeTypeCache, ICurrentTimeProvider currentTimeProvider, string path, Action<object, ConsumerContext> consumerContextInitializer = null)
    {
        if (messageBus is null) throw new ArgumentNullException(nameof(messageBus));

        _logger = messageBus.LoggerFactory.CreateLogger<MessageHandler>();
        _messageScopeFactory = messageScopeFactory;
        _currentTimeProvider = currentTimeProvider;
        _consumerContextInitializer = consumerContextInitializer;

        RuntimeTypeCache = runtimeTypeCache;
        MessageTypeResolver = messageTypeResolver;
        MessageHeadersFactory = messageHeadersFactory;
        MessageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
        Path = path ?? throw new ArgumentNullException(nameof(path));
    }

    public async Task<(object Response, Exception ResponseException, string RequestId)> DoHandle(object message, IReadOnlyDictionary<string, object> messageHeaders, IMessageTypeConsumerInvokerSettings consumerInvoker, CancellationToken cancellationToken, object nativeMessage = null, IServiceProvider currentServiceProvider = null)
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

        await using (var messageScope = _messageScopeFactory.CreateMessageScope(consumerInvoker.ParentSettings, message, currentServiceProvider))
        {
            if (messageHeaders != null && messageHeaders.TryGetHeader(ReqRespMessageHeaders.Expires, out DateTimeOffset? expires) && expires != null)
            {
                // Verify if the request/message is already expired
                var currentTime = _currentTimeProvider.CurrentTime;
                if (currentTime > expires.Value)
                {
                    // ToDo: Call interceptor

                    // Do not process the expired message
                    return (null, null, requestId);
                }
            }

            var consumerType = consumerInvoker.ConsumerType;
            var consumerInstance = messageScope.ServiceProvider.GetService(consumerType)
                ?? throw new ConfigurationMessageBusException($"Could not resolve consumer/handler type {consumerType} from the DI container. Please check that the configured type {consumerType} is registered within the DI container.");

            try
            {
                var context = new ConsumerContext
                {
                    Path = Path,
                    Headers = messageHeaders,
                    CancellationToken = cancellationToken,
                    Bus = MessageBus,
                    Consumer = consumerInstance,
                    ConsumerInvoker = consumerInvoker
                };
                _consumerContextInitializer?.Invoke(nativeMessage, context);

                var consumerInterceptors = RuntimeTypeCache.ConsumerInterceptorType.ResolveAll(messageScope.ServiceProvider, messageType);
                var handlerInterceptors = hasResponse ? RuntimeTypeCache.HandlerInterceptorType.ResolveAll(messageScope.ServiceProvider, (messageType, responseType)) : null;
                if (consumerInterceptors != null || handlerInterceptors != null)
                {
                    var pipeline = new ConsumerInterceptorPipeline(RuntimeTypeCache, this, message, responseType, context, consumerInvoker, consumerInterceptors: consumerInterceptors, handlerInterceptors: handlerInterceptors);
                    response = await pipeline.Next();
                }
                else
                {
                    // call without interceptors
                    response = await ExecuteConsumer(message, context, consumerInvoker, responseType).ConfigureAwait(false);
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

    public async Task<object> ExecuteConsumer(object message, IConsumerContext consumerContext, IMessageTypeConsumerInvokerSettings consumerInvoker, Type responseType)
    {
        if (RuntimeTypeCache.IsAssignableFrom(consumerContext.Consumer.GetType(), typeof(IConsumerWithContext)))
        {
            var consumerWithContext = (IConsumerWithContext)consumerContext.Consumer;
            consumerWithContext.Context = consumerContext;
        }

        // the consumer just subscribes to the message
        var task = consumerInvoker.ConsumerMethod(consumerContext.Consumer, message);
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