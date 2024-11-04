namespace SlimMessageBus.Host;

using System.Globalization;
using System.Runtime.ExceptionServices;

using SlimMessageBus.Host.Consumer;
using SlimMessageBus.Host.Services;

public abstract class MessageBusBase<TProviderSettings> : MessageBusBase where TProviderSettings : class
{
    public TProviderSettings ProviderSettings { get; }

    protected MessageBusBase(MessageBusSettings settings, TProviderSettings providerSettings) : base(settings)
    {
        ProviderSettings = providerSettings ?? throw new ArgumentNullException(nameof(providerSettings));
    }
}

public abstract class MessageBusBase : IDisposable, IAsyncDisposable,
    IMasterMessageBus,
    IMessageScopeFactory,
    IMessageHeadersFactory,
    IResponseProducer,
    IResponseConsumer,
    ITransportProducer,
    ITransportBulkProducer
{
    private readonly ILogger _logger;
    private CancellationTokenSource _cancellationTokenSource = new();
    private IMessageSerializer _serializer;
    private readonly MessageHeaderService _headerService;
    private readonly List<AbstractConsumer> _consumers = [];

    /// <summary>
    /// Special market reference that signifies a dummy producer settings for response types.
    /// </summary>
    protected static readonly ProducerSettings MarkerProducerSettingsForResponses = new();

    public RuntimeTypeCache RuntimeTypeCache { get; }

    public ILoggerFactory LoggerFactory { get; }

    public virtual MessageBusSettings Settings { get; }

    public virtual IMessageSerializer Serializer => _serializer ??= GetSerializer();

    public IMessageTypeResolver MessageTypeResolver { get; }

    /// <summary>
    /// Default <see cref="IMessageBusTarget"/> that corresponds to the root DI container, and pointing at self as the bus target.
    /// </summary>
    public virtual IMessageBusTarget MessageBusTarget { get; }

    protected ProducerByMessageTypeCache<ProducerSettings> ProducerSettingsByMessageType { get; private set; }

    protected IPendingRequestStore PendingRequestStore { get; set; }
    protected IPendingRequestManager PendingRequestManager { get; set; }

    public CancellationToken CancellationToken => _cancellationTokenSource.Token;

    #region Disposing

    protected bool IsDisposing { get; private set; }
    protected bool IsDisposed { get; private set; }

    #endregion

    private readonly object _initTaskLock = new();
    private Task _initTask = null;

    #region Start & Stop

    private readonly object _startLock = new();

    public bool IsStarted { get; private set; }

    protected bool IsStarting { get; private set; }
    protected bool IsStopping { get; private set; }

    #endregion

    public virtual string Name => Settings.Name ?? "Main";

    public IReadOnlyCollection<AbstractConsumer> Consumers => _consumers;

    protected MessageBusBase(MessageBusSettings settings)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));

        if (settings.ServiceProvider is null) throw new ConfigurationMessageBusException($"The bus {Name} has no {nameof(settings.ServiceProvider)} configured");

        // Try to resolve from DI. If not available, suppress logging by using the NullLoggerFactory
        LoggerFactory = settings.ServiceProvider.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;

        _logger = LoggerFactory.CreateLogger<MessageBusBase>();

        var messageTypeResolverType = settings.MessageTypeResolverType ?? typeof(IMessageTypeResolver);
        MessageTypeResolver = (IMessageTypeResolver)settings.ServiceProvider.GetService(messageTypeResolverType)
            ?? throw new ConfigurationMessageBusException($"The bus {Name} could not resolve the required type {messageTypeResolverType.Name} from {nameof(Settings.ServiceProvider)}");

        _headerService = new MessageHeaderService(LoggerFactory.CreateLogger<MessageHeaderService>(), Settings, MessageTypeResolver);

        RuntimeTypeCache = settings.ServiceProvider.GetRequiredService<RuntimeTypeCache>();

        MessageBusTarget = new MessageBusProxy(this, Settings.ServiceProvider);

        CurrentTimeProvider = settings.ServiceProvider.GetRequiredService<ICurrentTimeProvider>();

        PendingRequestManager = settings.ServiceProvider.GetRequiredService<IPendingRequestManager>();
        PendingRequestStore = PendingRequestManager.Store;
    }

    protected void AddInit(Task task)
    {
        lock (_initTaskLock)
        {
            var prevInitTask = _initTask;
            _initTask = prevInitTask?.ContinueWith(_ => task, CancellationToken) ?? task;
        }
    }

    /// <summary>
    /// Awaits (if any) bus intialization (e.g. topology provisining) before we can produce message into the bus.
    /// </summary>
    /// <returns></returns>
    protected async Task EnsureInitFinished()
    {
        var initTask = _initTask;
        if (initTask != null)
        {
            await initTask.ConfigureAwait(false);

            lock (_initTaskLock)
            {
                if (ReferenceEquals(_initTask, initTask))
                {
                    _initTask = null;
                }
            }
        }
    }

    protected virtual IMessageSerializer GetSerializer() => Settings.GetSerializer(Settings.ServiceProvider);

    protected virtual IMessageBusSettingsValidationService ValidationService { get => new DefaultMessageBusSettingsValidationService(Settings); }

    /// <summary>
    /// Called by the provider to initialize the bus.
    /// </summary>
    protected void OnBuildProvider()
    {
        ValidationService.AssertSettings();

        Build();

        // Notify the bus has been created - before any message can be produced
        AddInit(OnBusLifecycle(MessageBusLifecycleEventType.Created));

        // Auto start consumers if enabled
        if (Settings.AutoStartConsumers)
        {
            // Fire and forget start
            _ = Task.Run(async () =>
            {
                try
                {
                    await Start().ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Could not auto start consumers");
                }
            });
        }
    }

    protected virtual void Build()
    {
        ProducerSettingsByMessageType = new ProducerByMessageTypeCache<ProducerSettings>(_logger, BuildProducerByBaseMessageType(), RuntimeTypeCache);
    }

    private Dictionary<Type, ProducerSettings> BuildProducerByBaseMessageType()
    {
        var producerByBaseMessageType = Settings.Producers.ToDictionary(producerSettings => producerSettings.MessageType);

        foreach (var consumerSettings in Settings.Consumers.Where(x => x.ResponseType != null))
        {
            // A response type can be used across different requests hence TryAdd
            producerByBaseMessageType.TryAdd(consumerSettings.ResponseType, MarkerProducerSettingsForResponses);
        }
        return producerByBaseMessageType;
    }

    private IEnumerable<IMessageBusLifecycleInterceptor> _lifecycleInterceptors;

    private async Task OnBusLifecycle(MessageBusLifecycleEventType eventType)
    {
        _lifecycleInterceptors ??= Settings.ServiceProvider?.GetServices<IMessageBusLifecycleInterceptor>();
        if (_lifecycleInterceptors != null)
        {
            foreach (var i in _lifecycleInterceptors)
            {
                var task = i.OnBusLifecycle(eventType, MessageBusTarget);
                if (task != null)
                {
                    await task;
                }
            }
        }
    }

    public async Task Start()
    {
        lock (_startLock)
        {
            if (IsStarting || IsStarted)
            {
                return;
            }
            IsStarting = true;
        }

        try
        {
            await EnsureInitFinished();

            _logger.LogInformation("Starting consumers for {BusName} bus...", Name);
            await OnBusLifecycle(MessageBusLifecycleEventType.Starting).ConfigureAwait(false);

            await CreateConsumers();
            await OnStart().ConfigureAwait(false);
            await Task.WhenAll(_consumers.Select(x => x.Start())).ConfigureAwait(false);

            await OnBusLifecycle(MessageBusLifecycleEventType.Started).ConfigureAwait(false);
            _logger.LogInformation("Started consumers for {BusName} bus", Name);

            lock (_startLock)
            {
                IsStarted = true;
            }
        }
        finally
        {
            lock (_startLock)
            {
                IsStarting = false;
            }
        }
    }

    public async Task Stop()
    {
        lock (_startLock)
        {
            if (IsStopping || !IsStarted)
            {
                return;
            }
            IsStopping = true;
        }

        try
        {
            await EnsureInitFinished();

            _logger.LogInformation("Stopping consumers for {BusName} bus...", Name);
            await OnBusLifecycle(MessageBusLifecycleEventType.Stopping).ConfigureAwait(false);

            await Task.WhenAll(_consumers.Select(x => x.Stop())).ConfigureAwait(false);
            await OnStop().ConfigureAwait(false);
            await DestroyConsumers().ConfigureAwait(false);

            await OnBusLifecycle(MessageBusLifecycleEventType.Stopped).ConfigureAwait(false);
            _logger.LogInformation("Stopped consumers for {BusName} bus", Name);

            lock (_startLock)
            {
                IsStarted = false;
            }
        }
        finally
        {
            lock (_startLock)
            {
                IsStopping = false;
            }
        }
    }

    protected internal virtual Task OnStart() => Task.CompletedTask;
    protected internal virtual Task OnStop() => Task.CompletedTask;

    protected void AssertActive()
    {
        if (IsDisposed)
        {
            throw new MessageBusException("The message bus is disposed at this time");
        }
    }

    protected virtual void AssertRequestResponseConfigured()
    {
        if (Settings.RequestResponse == null)
        {
            throw new SendMessageBusException("An attempt to send request when request/response communication was not configured for the message bus. Ensure you configure the bus properly before the application starts.");
        }
    }

    #region Implementation of IDisposable and IAsyncDisposable

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeAsyncInternal().ConfigureAwait(false).GetAwaiter().GetResult();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncInternal().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    private async ValueTask DisposeAsyncInternal()
    {
        if (!IsDisposed && !IsDisposing)
        {
            IsDisposing = true;
            try
            {
                await DisposeAsyncCore().ConfigureAwait(false);
            }
            finally
            {
                IsDisposed = true;
                IsDisposing = false;
            }
        }
    }

    /// <summary>
    /// Stops the consumers and disposes of internal bus objects.
    /// </summary>
    /// <returns></returns>
    protected async virtual ValueTask DisposeAsyncCore()
    {
        await Stop().ConfigureAwait(false);

        if (_cancellationTokenSource != null)
        {
            await _cancellationTokenSource.CancelAsync();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }
    }

    protected virtual Task CreateConsumers()
    {
        _logger.LogInformation("Creating consumers for {BusName} bus...", Name);
        return Task.CompletedTask;
    }

    protected async virtual Task DestroyConsumers()
    {
        _logger.LogInformation("Destroying consumers for {BusName} bus...", Name);

        foreach (var consumer in _consumers)
        {
            await consumer.DisposeSilently("Consumer", _logger).ConfigureAwait(false);
        }
        _consumers.Clear();
    }

    #endregion

    protected void AddConsumer(AbstractConsumer consumer) => _consumers.Add(consumer);

    public ICurrentTimeProvider CurrentTimeProvider { get; protected set; }

    protected ProducerSettings GetProducerSettings(Type messageType)
    {
        var producerSettings = ProducerSettingsByMessageType[messageType];
        if (producerSettings == null && !ReferenceEquals(producerSettings, MarkerProducerSettingsForResponses))
        {
            throw new ProducerMessageBusException($"Message of type {messageType} was not registered as a supported produce message. Please check your MessageBus configuration and include this type or one of its base types.");
        }
        return producerSettings;
    }

    protected virtual string GetDefaultPath(Type messageType, ProducerSettings producerSettings)
    {
        if (producerSettings == null) throw new ArgumentNullException(nameof(producerSettings));

        var path = producerSettings.DefaultPath
            ?? throw new ProducerMessageBusException($"An attempt to produce message of type {messageType} without specifying path, but there was no default path configured. Double check your configuration.");

        _logger.LogDebug("Applying default path {Path} for message type {MessageType}", path, messageType);
        return path;
    }

    public abstract Task ProduceToTransport(
        object message,
        Type messageType,
        string path,
        IDictionary<string, object> messageHeaders,
        IMessageBusTarget targetBus,
        CancellationToken cancellationToken);

    protected void OnProduceToTransport(object message,
        Type messageType,
        string path,
        IDictionary<string, object> messageHeaders)
        => _logger.LogDebug("Producing message {Message} of type {MessageType} to path {Path}", message, messageType, path);

    public virtual int? MaxMessagesPerTransaction => null;

    public async virtual Task<ProduceToTransportBulkResult<T>> ProduceToTransportBulk<T>(
        IReadOnlyCollection<T> envelopes,
        string path,
        IMessageBusTarget targetBus,
        CancellationToken cancellationToken)
        where T : BulkMessageEnvelope
    {
        var dispatched = new List<T>();
        try
        {
            foreach (var envelope in envelopes)
            {
                await ProduceToTransport(envelope.Message, envelope.MessageType, path, envelope.Headers, targetBus, cancellationToken)
                    .ConfigureAwait(false);

                dispatched.Add(envelope);
            }
            return new(dispatched, null);
        }
        catch (Exception ex)
        {
            return new(dispatched, ex);
        }
    }

    public async virtual Task ProducePublish(object message, string path = null, IDictionary<string, object> headers = null, IMessageBusTarget targetBus = null, CancellationToken cancellationToken = default)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));
        AssertActive();
        await EnsureInitFinished();

        // check if the cancellation was already requested
        cancellationToken.ThrowIfCancellationRequested();

        var messageType = message.GetType();

        // check if the message type passed is in reality a collection of messages
        var collectionInfo = RuntimeTypeCache.GetCollectionTypeInfo(messageType);
        if (collectionInfo != null)
        {
            messageType = collectionInfo.ItemType;
        }

        var producerSettings = GetProducerSettings(messageType);
        path ??= GetDefaultPath(producerSettings.MessageType, producerSettings);

        if (collectionInfo != null)
        {
            // produce multiple messages to transport
            var messages = collectionInfo
                .ToCollection(message)
                .Select(m => new BulkMessageEnvelope(m, messageType, GetMessageHeaders(m, headers, producerSettings)))
                .ToList();

            var result = await ProduceToTransportBulk(messages, path, targetBus, cancellationToken);

            if (result.Exception != null)
            {
                if (result.Exception is ProducerMessageBusException)
                {
                    // We want to pass the same exception to the sender as it happened in the handler/consumer
                    ExceptionDispatchInfo.Capture(result.Exception).Throw();
                }
                throw new ProducerMessageBusException(GetProducerErrorMessage(path, message, messageType, result.Exception), result.Exception);
            }
            return;
        }

        var messageHeaders = GetMessageHeaders(message, headers, producerSettings);

        // Producer interceptors do not work on collections (batch publish)
        var serviceProvider = targetBus?.ServiceProvider ?? Settings.ServiceProvider;

        var producerInterceptors = RuntimeTypeCache.ProducerInterceptorType.ResolveAll(serviceProvider, messageType);
        var publishInterceptors = RuntimeTypeCache.PublishInterceptorType.ResolveAll(serviceProvider, messageType);
        if (producerInterceptors != null || publishInterceptors != null)
        {
            var context = new PublishContext
            {
                Path = path,
                CancellationToken = cancellationToken,
                Headers = messageHeaders,
                Bus = new MessageBusProxy(this, serviceProvider),
                ProducerSettings = producerSettings
            };

            var pipeline = new PublishInterceptorPipeline(this, RuntimeTypeCache, message, producerSettings, targetBus, context, producerInterceptors: producerInterceptors, publishInterceptors: publishInterceptors);
            await pipeline.Next();
            return;
        }

        // produce a single message to transport
        await ProduceToTransport(message, messageType, path, messageHeaders, targetBus, cancellationToken);
    }

    protected static string GetProducerErrorMessage(string path, object message, Type messageType, Exception ex)
        => $"Producing message {message} of type {messageType?.Name} to path {path} resulted in error: {ex.Message}";

    /// <summary>
    /// Create an instance of message headers.
    /// </summary>
    /// <returns></returns>
    public virtual IDictionary<string, object> CreateHeaders() => new Dictionary<string, object>(10);

    private IDictionary<string, object> GetMessageHeaders(object message, IDictionary<string, object> headers, ProducerSettings producerSettings)
    {
        var messageHeaders = CreateHeaders();
        if (messageHeaders != null)
        {
            _headerService.AddMessageHeaders(messageHeaders, headers, message, producerSettings);
        }
        return messageHeaders;
    }

    protected virtual TimeSpan GetDefaultRequestTimeout(Type requestType, ProducerSettings producerSettings)
    {
        if (producerSettings == null) throw new ArgumentNullException(nameof(producerSettings));

        var timeout = producerSettings.Timeout ?? Settings.RequestResponse.Timeout;
        _logger.LogDebug("Applying default timeout {MessageTimeout} for message type {MessageType}", timeout, requestType);
        return timeout;
    }

    public virtual async Task<TResponse> ProduceSend<TResponse>(object request, string path = null, IDictionary<string, object> headers = null, TimeSpan? timeout = null, IMessageBusTarget targetBus = null, CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        AssertActive();
        AssertRequestResponseConfigured();
        await EnsureInitFinished();

        // check if the cancellation was already requested
        cancellationToken.ThrowIfCancellationRequested();

        var requestType = request.GetType();
        var responseType = typeof(TResponse);

        var producerSettings = GetProducerSettings(requestType);

        path ??= GetDefaultPath(requestType, producerSettings);
        timeout ??= GetDefaultRequestTimeout(requestType, producerSettings);

        var created = CurrentTimeProvider.CurrentTime;
        var expires = created.Add(timeout.Value);

        // generate the request guid
        var requestId = GenerateRequestId();

        var requestHeaders = CreateHeaders();
        if (requestHeaders != null)
        {
            _headerService.AddMessageHeaders(requestHeaders, headers, request, producerSettings);
            if (requestId != null)
            {
                requestHeaders.SetHeader(ReqRespMessageHeaders.RequestId, requestId);
            }
            requestHeaders.SetHeader(ReqRespMessageHeaders.Expires, expires);
        }

        var serviceProvider = targetBus?.ServiceProvider ?? Settings.ServiceProvider;

        var producerInterceptors = RuntimeTypeCache.ProducerInterceptorType.ResolveAll(serviceProvider, requestType);
        var sendInterceptors = RuntimeTypeCache.SendInterceptorType.ResolveAll(serviceProvider, (requestType, responseType));
        if (producerInterceptors != null || sendInterceptors != null)
        {
            var context = new SendContext
            {
                Path = path,
                CancellationToken = cancellationToken,
                Headers = requestHeaders,
                Bus = new MessageBusProxy(this, serviceProvider),
                ProducerSettings = producerSettings,
                Created = created,
                Expires = expires,
                RequestId = requestId,
            };

            var pipeline = new SendInterceptorPipeline<TResponse>(this, request, producerSettings, targetBus, context, producerInterceptors: producerInterceptors, sendInterceptors: sendInterceptors);
            return await pipeline.Next();
        }

        return await SendInternal<TResponse>(request, path, requestType, responseType, producerSettings, created, expires, requestId, requestHeaders, targetBus, cancellationToken);
    }

    protected async internal virtual Task<TResponseMessage> SendInternal<TResponseMessage>(object request, string path, Type requestType, Type responseType, ProducerSettings producerSettings, DateTimeOffset created, DateTimeOffset expires, string requestId, IDictionary<string, object> requestHeaders, IMessageBusTarget targetBus, CancellationToken cancellationToken)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (producerSettings == null) throw new ArgumentNullException(nameof(producerSettings));

        // record the request state
        var requestState = new PendingRequestState(requestId, request, requestType, responseType, created, expires, cancellationToken);
        PendingRequestStore.Add(requestState);

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("Added to PendingRequests, total is {RequestCount}", PendingRequestStore.GetCount());
        }

        try
        {
            _logger.LogDebug("Sending request message {MessageType} to path {Path} with reply to {ReplyTo}", requestState, path, Settings.RequestResponse.Path);

            if (requestHeaders != null)
            {
                requestHeaders.SetHeader(ReqRespMessageHeaders.ReplyTo, Settings.RequestResponse.Path);
                _headerService.AddMessageTypeHeader(request, requestHeaders);
            }

            await ProduceToTransport(request, producerSettings.MessageType, path, requestHeaders, targetBus, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogDebug(e, "Publishing of request message failed");
            // remove from registry
            PendingRequestStore.Remove(requestId);
            throw;
        }

        // convert Task<object> to Task<TResponseMessage>
        var responseUntyped = await requestState.TaskCompletionSource.Task.ConfigureAwait(false);
        return (TResponseMessage)responseUntyped;
    }

    public virtual Task ProduceResponse(string requestId, object request, IReadOnlyDictionary<string, object> requestHeaders, object response, Exception responseException, IMessageTypeConsumerInvokerSettings consumerInvoker, CancellationToken cancellationToken)
    {
        if (requestHeaders == null) throw new ArgumentNullException(nameof(requestHeaders));
        if (consumerInvoker == null) throw new ArgumentNullException(nameof(consumerInvoker));

        var responseType = consumerInvoker.ParentSettings.ResponseType;
        if (!requestHeaders.TryGetHeader(ReqRespMessageHeaders.ReplyTo, out object replyTo))
        {
            _logger.LogDebug($$"""Skipping sending response {Response} of type {MessageType} as the header {{ReqRespMessageHeaders.ReplyTo}} is missing for RequestId: {RequestId}""", response, responseType, requestId);
            return Task.CompletedTask;
        }

        _logger.LogDebug("Sending the response {Response} of type {MessageType} for RequestId: {RequestId}...", response, responseType, requestId);

        var responseHeaders = CreateHeaders();
        responseHeaders.SetHeader(ReqRespMessageHeaders.RequestId, requestId);
        if (responseException != null)
        {
            responseHeaders.SetHeader(ReqRespMessageHeaders.Error, responseException.Message);
        }

        _headerService.AddMessageTypeHeader(response, responseHeaders);

        return ProduceToTransport(response, responseType, (string)replyTo, responseHeaders, null, cancellationToken);
    }

    /// <summary>
    /// Should be invoked by the concrete bus implementation whenever there is a message arrived on the reply to topic.
    /// </summary>
    /// <param name="responsePayload"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    public virtual Task<Exception> OnResponseArrived(byte[] responsePayload, string path, IReadOnlyDictionary<string, object> responseHeaders)
    {
        if (!responseHeaders.TryGetHeader(ReqRespMessageHeaders.RequestId, out string requestId))
        {
            _logger.LogError("The response message arriving on path {Path} did not have the {HeaderName} header. Unable to math the response with the request. This likely indicates a misconfiguration.", path, ReqRespMessageHeaders.RequestId);
            return Task.FromResult<Exception>(null);
        }

        Exception responseException = null;
        if (responseHeaders.TryGetHeader(ReqRespMessageHeaders.Error, out string errorMessage))
        {
            responseException = new RequestHandlerFaultedMessageBusException(errorMessage);
        }

        var requestState = PendingRequestStore.GetById(requestId);
        if (requestState == null)
        {
            _logger.LogDebug("The response message for request id {RequestId} arriving on path {Path} will be disregarded. Either the request had already expired, had been cancelled or it was already handled (this response message is a duplicate).", requestId, path);

            // ToDo: add and API hook to these kind of situation
            return Task.FromResult<Exception>(null);
        }

        try
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                var tookTimespan = CurrentTimeProvider.CurrentTime.Subtract(requestState.Created);
                _logger.LogDebug("Response arrived for {Request} on path {Path} (time: {RequestTime} ms)", requestState, path, tookTimespan);
            }

            if (responseException != null)
            {
                // error response arrived
                _logger.LogDebug(responseException, "Response arrived for {Request} on path {Path} with error: {ResponseError}", requestState, path, responseException.Message);

                requestState.TaskCompletionSource.TrySetException(responseException);
            }
            else
            {
                // response arrived
                try
                {
                    // deserialize the response message
                    var response = responsePayload != null
                        ? Serializer.Deserialize(requestState.ResponseType, responsePayload)
                        : null;

                    // resolve the response
                    requestState.TaskCompletionSource.TrySetResult(response);
                }
                catch (Exception e)
                {
                    _logger.LogDebug(e, "Could not deserialize the response message for {Request} arriving on path {Path}", requestState, path);
                    requestState.TaskCompletionSource.TrySetException(e);
                }
            }
        }
        finally
        {
            // remove the request from the queue
            PendingRequestStore.Remove(requestId);
        }
        return Task.FromResult<Exception>(null);
    }

    /// <summary>
    /// Generates unique request IDs
    /// </summary>
    /// <returns></returns>
    protected virtual string GenerateRequestId() => Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

    public virtual bool IsMessageScopeEnabled(ConsumerSettings consumerSettings, IDictionary<string, object> consumerContextProperties)
        => consumerSettings.IsMessageScopeEnabled ?? Settings.IsMessageScopeEnabled ?? true;

    public virtual IMessageScope CreateMessageScope(ConsumerSettings consumerSettings, object message, IDictionary<string, object> consumerContextProperties, IServiceProvider currentServiceProvider)
    {
        var createMessageScope = IsMessageScopeEnabled(consumerSettings, consumerContextProperties);

        if (createMessageScope)
        {
            _logger.LogDebug("Creating message scope for {Message} of type {MessageType}", message, message.GetType());
        }
        return new MessageScopeWrapper(currentServiceProvider ?? Settings.ServiceProvider, createMessageScope);
    }

    public virtual Task ProvisionTopology() => Task.CompletedTask;
}
