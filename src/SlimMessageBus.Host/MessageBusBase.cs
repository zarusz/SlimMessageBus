namespace SlimMessageBus.Host
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using SlimMessageBus.Host.Collections;
    using SlimMessageBus.Host.Config;
    using SlimMessageBus.Host.DependencyResolver;
    using SlimMessageBus.Host.Interceptor;
    using SlimMessageBus.Host.Serialization;

    public abstract class MessageBusBase : IMasterMessageBus, IAsyncDisposable
    {
        private readonly ILogger _logger;
        private CancellationTokenSource _cancellationTokenSource = new();
        public RuntimeTypeCache RuntimeTypeCache { get; }

        public ILoggerFactory LoggerFactory { get; }

        public virtual MessageBusSettings Settings { get; }

        public virtual IMessageSerializer Serializer => Settings.Serializer;

        protected ProducerByMessageTypeCache<ProducerSettings> ProducerSettingsByMessageType { get; private set; }

        protected IPendingRequestStore PendingRequestStore { get; set; }
        protected PendingRequestManager PendingRequestManager { get; set; }

        public CancellationToken CancellationToken => _cancellationTokenSource.Token;

        protected bool IsDisposing { get; private set; }
        protected bool IsDisposed { get; private set; }

        protected MessageBusBase(MessageBusSettings settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));

            // Use the configured logger factory, if not provided try to resolve from DI, if also not available supress logging using the NullLoggerFactory
            LoggerFactory = settings.LoggerFactory
                ?? (ILoggerFactory)settings.DependencyResolver?.Resolve(typeof(ILoggerFactory))
                ?? NullLoggerFactory.Instance;

            _logger = LoggerFactory.CreateLogger<MessageBusBase>();

            RuntimeTypeCache = new RuntimeTypeCache();
        }

        /// <summary>
        /// Called by the provider to initialize the bus.
        /// </summary>
        protected void OnBuildProvider()
        {
            AssertSettings();
            Build();

            if (Settings.AutoStartConsumers)
            {
                // Fire and forget - start
                _ = Start();
            }
        }

        /// <summary>
        /// Special market reference that signifies a dummy producer settings for response types.
        /// </summary>
        private static readonly ProducerSettings MarkerProducerSettingsForResponses = new();

        protected virtual void Build()
        {
            ProducerSettingsByMessageType = new ProducerByMessageTypeCache<ProducerSettings>(_logger, BuildProducerByBaseMessageType());

            PendingRequestStore = new InMemoryPendingRequestStore();
            PendingRequestManager = new PendingRequestManager(PendingRequestStore, () => CurrentTime, TimeSpan.FromSeconds(1), LoggerFactory, request =>
            {
                // Execute the event hook
                try
                {
                    (Settings.RequestResponse.OnMessageExpired ?? Settings.OnMessageExpired)?.Invoke(this, Settings.RequestResponse, request, null);
                }
                catch (Exception eh)
                {
                    HookFailed(_logger, eh, nameof(IConsumerEvents.OnMessageExpired));
                }
            });
            PendingRequestManager.Start();
        }

        private IDictionary<Type, ProducerSettings> BuildProducerByBaseMessageType()
        {
            var producerByBaseMessageType = new Dictionary<Type, ProducerSettings>();
            foreach (var producerSettings in Settings.Producers)
            {
                producerByBaseMessageType.Add(producerSettings.MessageType, producerSettings);
            }
            foreach (var consumerSettings in Settings.Consumers.Where(x => x.ResponseType != null))
            {
                // A response type can be used across different requests hence TryAdd
                producerByBaseMessageType.TryAdd(consumerSettings.ResponseType, MarkerProducerSettingsForResponses);
            }
            return producerByBaseMessageType;
        }

        public bool IsStarted { get; private set; }

        public async Task Start()
        {
            if (!IsStarted)
            {
                _logger.LogInformation("Starting consumers...");
                await OnStart();
                _logger.LogInformation("Started consumers");

                IsStarted = true;
            }
        }

        public async Task Stop()
        {
            if (IsStarted)
            {
                _logger.LogInformation("Stopping consumers...");
                await OnStop();
                _logger.LogInformation("Stopped consumers");

                IsStarted = false;
            }
        }

        protected virtual Task OnStart() => Task.CompletedTask;
        protected virtual Task OnStop() => Task.CompletedTask;

        public static void HookFailed(ILogger logger, Exception eh, string name)
        {
            // When the hook itself error out, catch the exception
            logger.LogError(eh, "{HookName} method failed", name);
        }

        protected virtual void AssertSettings()
        {
            AssertProducers();
            foreach (var consumerSettings in Settings.Consumers)
            {
                AssertConsumerSettings(consumerSettings);
            }
            AssertSerializerSettings();
            AssertDepencendyResolverSettings();
            AssertRequestResponseSettings();
        }

        protected virtual void AssertProducers()
        {
            var duplicateMessageTypeProducer = Settings.Producers.GroupBy(x => x.MessageType).Where(x => x.Count() > 1).Select(x => x.FirstOrDefault()).FirstOrDefault();
            if (duplicateMessageTypeProducer != null)
            {
                throw new ConfigurationMessageBusException($"The produced message type {duplicateMessageTypeProducer.MessageType} was declared more than once (check the {nameof(MessageBusBuilder.Produce)} configuration)");
            }
        }

        protected virtual void AssertConsumerSettings(ConsumerSettings consumerSettings)
        {
            if (consumerSettings == null) throw new ArgumentNullException(nameof(consumerSettings));

            Assert.IsNotNull(consumerSettings.Path,
                () => new ConfigurationMessageBusException($"The {nameof(ConsumerSettings)}.{nameof(consumerSettings.Path)} is not set"));
            Assert.IsNotNull(consumerSettings.MessageType,
                () => new ConfigurationMessageBusException($"The {nameof(ConsumerSettings)}.{nameof(consumerSettings.MessageType)} is not set"));
            Assert.IsNotNull(consumerSettings.ConsumerType,
                () => new ConfigurationMessageBusException($"The {nameof(ConsumerSettings)}.{nameof(consumerSettings.ConsumerType)} is not set"));
            Assert.IsNotNull(consumerSettings.ConsumerMethod,
                () => new ConfigurationMessageBusException($"The {nameof(ConsumerSettings)}.{nameof(consumerSettings.ConsumerMethod)} is not set"));

            if (consumerSettings.ConsumerMode == ConsumerMode.RequestResponse)
            {
                Assert.IsNotNull(consumerSettings.ResponseType,
                    () => new ConfigurationMessageBusException($"The {nameof(ConsumerSettings)}.{nameof(consumerSettings.ResponseType)} is not set"));

                Assert.IsNotNull(consumerSettings.ConsumerMethodResult,
                    () => new ConfigurationMessageBusException($"The {nameof(ConsumerSettings)}.{nameof(consumerSettings.ConsumerMethodResult)} is not set"));
            }
        }

        protected virtual void AssertSerializerSettings()
        {
            Assert.IsNotNull(Settings.Serializer,
                () => new ConfigurationMessageBusException($"The {nameof(MessageBusSettings)}.{nameof(MessageBusSettings.Serializer)} is not set"));
        }

        protected virtual void AssertDepencendyResolverSettings()
        {
            Assert.IsNotNull(Settings.DependencyResolver,
                () => new ConfigurationMessageBusException($"The {nameof(MessageBusSettings)}.{nameof(MessageBusSettings.DependencyResolver)} is not set"));
        }

        protected virtual void AssertRequestResponseSettings()
        {
            if (Settings.RequestResponse != null)
            {
                Assert.IsNotNull(Settings.RequestResponse.Path,
                    () => new ConfigurationMessageBusException("Request-response: name was not set"));
            }
        }

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
                throw new PublishMessageBusException("An attempt to send request when request/response communication was not configured for the message bus. Ensure you configure the bus properly before the application starts.");
            }
        }

        #region Implementation of IDisposable and IAsyncDisposable

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
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
                    IsDisposing = false;
                    IsDisposed = true;
                }
            }
        }

        /// <summary>
        /// Stops the consumers and disposes of internal bus objects.
        /// </summary>
        /// <returns></returns>
        protected virtual async ValueTask DisposeAsyncCore()
        {
            await Stop();

            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }

            if (PendingRequestManager != null)
            {
                PendingRequestManager.Dispose();
                PendingRequestManager = null;
            }
        }

        #endregion

        public virtual DateTimeOffset CurrentTime => DateTimeOffset.UtcNow;

        protected ProducerSettings GetProducerSettings(Type messageType)
        {
            var producerSettings = ProducerSettingsByMessageType.GetProducer(messageType);
            if (producerSettings == null && !ReferenceEquals(producerSettings, MarkerProducerSettingsForResponses))
            {
                throw new PublishMessageBusException($"Message of type {messageType} was not registered as a supported produce message. Please check your MessageBus configuration and include this type or one of its base types.");
            }
            return producerSettings;
        }

        protected virtual string GetDefaultPath(Type messageType, ProducerSettings producerSettings)
        {
            if (producerSettings == null) throw new ArgumentNullException(nameof(producerSettings));

            var path = producerSettings.DefaultPath
                ?? throw new PublishMessageBusException($"An attempt to produce message of type {messageType} without specifying path, but there was no default path configured. Double check your configuration.");

            _logger.LogDebug("Applying default path {Path} for message type {MessageType}", path, messageType);
            return path;
        }

        public abstract Task ProduceToTransport(object message, string path, byte[] messagePayload, IDictionary<string, object> messageHeaders = null, CancellationToken cancellationToken = default);

        public virtual async Task Publish(object message, string path = null, IDictionary<string, object> headers = null, CancellationToken cancellationToken = default, IDependencyResolver currentDependencyResolver = null)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            AssertActive();

            var messageType = message.GetType();
            var producerSettings = GetProducerSettings(messageType);

            path ??= GetDefaultPath(producerSettings.MessageType, producerSettings);

            var messageHeaders = CreateHeaders();
            AddMessageHeaders(messageHeaders, headers, message, producerSettings);

            var resolver = currentDependencyResolver ?? Settings.DependencyResolver;

            var producerInterceptors = RuntimeTypeCache.ProducerInterceptorType.ResolveAll(resolver, messageType);
            var publishInterceptors = RuntimeTypeCache.PublishInterceptorType.ResolveAll(resolver, messageType);
            if (producerInterceptors != null || publishInterceptors != null)
            {
                var next = () => PublishInternal(message, path, messageHeaders, cancellationToken, producerSettings);

                // Note: In this particular order - we want the publish interceptor to be wrapped by produce interceptor

                if (publishInterceptors != null)
                {
                    var publishInterceptorType = RuntimeTypeCache.PublishInterceptorType.Get(messageType);
                    foreach (var publishInterceptor in publishInterceptors.OfType<IInterceptor>().OrderBy(x => x.GetOrder()))
                    {
                        // The params follow IPublishInterceptor<>.OnHandle parameters
                        var interceptorParams = new object[] { message, cancellationToken, next, this, path, headers };
                        next = () => (Task)publishInterceptorType.Method.Invoke(publishInterceptor, interceptorParams);
                    }
                }

                if (producerInterceptors != null)
                {
                    // IProducerInterceptor<,> requires next() to return Task<object> 
                    var nextSnapshot = next;
                    var nextWithResult = () => nextSnapshot().ContinueWith<object>(x => null);

                    var producerInterceptorType = RuntimeTypeCache.ProducerInterceptorType.Get(messageType);
                    foreach (var producerInterceptor in producerInterceptors.OfType<IInterceptor>().OrderBy(x => x.GetOrder()))
                    {
                        // The params follow IPublishInterceptor<>.OnHandle parameters
                        var interceptorParams = new object[] { message, cancellationToken, nextWithResult, this, path, headers };
                        nextWithResult = () => (Task<object>)producerInterceptorType.Method.Invoke(producerInterceptor, interceptorParams);
                    }

                    next = nextWithResult;
                }

                await next();
                return;
            }

            await PublishInternal(message, path, messageHeaders, cancellationToken, producerSettings);
        }

        protected virtual Task PublishInternal(object message, string path, IDictionary<string, object> messageHeaders, CancellationToken cancellationToken, ProducerSettings producerSettings)
        {
            OnProducedHook(message, path, producerSettings);

            var payload = Serializer.Serialize(producerSettings.MessageType, message);

            _logger.LogDebug("Producing message {Message} of type {MessageType} to path {Path}", message, producerSettings.MessageType, path);
            return ProduceToTransport(message, path, payload, messageHeaders, cancellationToken);
        }

        private void AddMessageHeaders(IDictionary<string, object> messageHeaders, IDictionary<string, object> headers, object message, ProducerSettings producerSettings)
        {
            if (headers != null)
            {
                // Add user specific headers
                foreach (var (key, value) in headers)
                {
                    messageHeaders[key] = value;
                }
            }

            AddMessageTypeHeader(message, messageHeaders);
            // Call header hook
            producerSettings.HeaderModifier?.Invoke(messageHeaders, message);
            // Call header hook
            Settings.HeaderModifier?.Invoke(messageHeaders, message);
        }

        private void AddMessageTypeHeader(object message, IDictionary<string, object> headers)
        {
            if (message != null)
            {
                headers.SetHeader(MessageHeaders.MessageType, Settings.MessageTypeResolver.ToName(message.GetType()));
            }
        }

        /// <summary>
        /// Create an instance of message headers.
        /// </summary>
        /// <returns></returns>
        public virtual IDictionary<string, object> CreateHeaders() => new Dictionary<string, object>(10);

        protected virtual TimeSpan GetDefaultRequestTimeout(Type requestType, ProducerSettings producerSettings)
        {
            if (producerSettings == null) throw new ArgumentNullException(nameof(producerSettings));

            var timeout = producerSettings.Timeout ?? Settings.RequestResponse.Timeout;
            _logger.LogDebug("Applying default timeout {MessageTimeout} for message type {MessageType}", timeout, requestType);
            return timeout;
        }

        public virtual async Task<TResponse> SendInternal<TResponse>(object request, TimeSpan? timeout, string path, IDictionary<string, object> headers, CancellationToken cancellationToken, IDependencyResolver currentDependencyResolver = null)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            AssertActive();
            AssertRequestResponseConfigured();

            // check if the cancellation was already requested
            cancellationToken.ThrowIfCancellationRequested();

            var requestType = request.GetType();
            var responseType = typeof(TResponse);
            var producerSettings = GetProducerSettings(requestType);

            path ??= GetDefaultPath(requestType, producerSettings);
            timeout ??= GetDefaultRequestTimeout(requestType, producerSettings);

            OnProducedHook(request, path, producerSettings);

            var created = CurrentTime;
            var expires = created.Add(timeout.Value);

            // generate the request guid
            var requestId = GenerateRequestId();

            var requestHeaders = CreateHeaders();
            AddMessageHeaders(requestHeaders, headers, request, producerSettings);
            requestHeaders.SetHeader(ReqRespMessageHeaders.RequestId, requestId);
            requestHeaders.SetHeader(ReqRespMessageHeaders.Expires, expires);

            var resolver = currentDependencyResolver ?? Settings.DependencyResolver;

            var producerInterceptors = RuntimeTypeCache.ProducerInterceptorType.ResolveAll(resolver, requestType);
            var sendInterceptors = RuntimeTypeCache.SendInterceptorType.ResolveAll(resolver, requestType, responseType);
            if (producerInterceptors != null || sendInterceptors != null)
            {
                var next = () => SendInternal<TResponse>(request, path, requestType, responseType, producerSettings, created, expires, requestId, requestHeaders, cancellationToken);

                // Note: In this particular order - we want the send interceptor to be wrapped by produce interceptor

                if (sendInterceptors != null)
                {
                    var sendInterceptorType = RuntimeTypeCache.SendInterceptorType.Get(requestType, responseType);
                    foreach (var sendInterceptor in sendInterceptors.OfType<IInterceptor>().OrderBy(x => x.GetOrder()))
                    {
                        // The params follow IPublishInterceptor<>.OnHandle parameters
                        var interceptorParams = new object[] { request, cancellationToken, next, this, path, headers };
                        next = () => (Task<TResponse>)sendInterceptorType.Method.Invoke(sendInterceptor, interceptorParams);
                    }
                }

                if (producerInterceptors != null)
                {
                    // The IProducerInterceptor<,> requires next() to return Task<object>
                    var nextSnapshot = next;
                    var nextWithObjectResult = () => nextSnapshot().ContinueWith(x => (object)x.Result);

                    var producerInterceptorType = RuntimeTypeCache.ProducerInterceptorType.Get(requestType);
                    foreach (var producerInterceptor in producerInterceptors.OfType<IInterceptor>().OrderBy(x => x.GetOrder()))
                    {
                        // The params follow IPublishInterceptor<>.OnHandle parameters
                        var interceptorParams = new object[] { request, cancellationToken, nextWithObjectResult, this, path, headers };
                        nextWithObjectResult = () => (Task<object>)producerInterceptorType.Method.Invoke(producerInterceptor, interceptorParams);
                    }

                    // Cast back to Task<TResponse>
                    next = () => nextWithObjectResult().ContinueWith(x => (TResponse)x.Result);
                }

                return await next();
            }

            return await SendInternal<TResponse>(request, path, requestType, responseType, producerSettings, created, expires, requestId, requestHeaders, cancellationToken);
        }

        private async Task<TResponseMessage> SendInternal<TResponseMessage>(object request, string path, Type requestType, Type responseType, ProducerSettings producerSettings, DateTimeOffset created, DateTimeOffset expires, string requestId, IDictionary<string, object> requestHeaders, CancellationToken cancellationToken)
        {
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
                await ProduceRequest(request, requestHeaders, path, producerSettings).ConfigureAwait(false);
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

        private void OnProducedHook(object message, string name, ProducerSettings producerSettings)
        {
            try
            {
                producerSettings?.OnMessageProduced?.Invoke(this, producerSettings, message, name);
                Settings.OnMessageProduced?.Invoke(this, producerSettings, message, name);
            }
            catch (Exception eh)
            {
                HookFailed(_logger, eh, nameof(IProducerEvents.OnMessageProduced));
            }
        }

        public virtual Task ProduceRequest(object request, IDictionary<string, object> requestHeaders, string path, ProducerSettings producerSettings)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (requestHeaders == null) throw new ArgumentNullException(nameof(requestHeaders));
            if (producerSettings == null) throw new ArgumentNullException(nameof(producerSettings));

            var requestPayload = Serializer.Serialize(producerSettings.MessageType, request);

            requestHeaders.SetHeader(ReqRespMessageHeaders.ReplyTo, Settings.RequestResponse.Path);
            AddMessageTypeHeader(request, requestHeaders);

            return ProduceToTransport(request, path, requestPayload, requestHeaders);
        }

        public virtual Task ProduceResponse(object request, IDictionary<string, object> requestHeaders, object response, IDictionary<string, object> responseHeaders, ConsumerSettings consumerSettings)
        {
            if (requestHeaders == null) throw new ArgumentNullException(nameof(requestHeaders));
            if (responseHeaders == null) throw new ArgumentNullException(nameof(responseHeaders));
            if (consumerSettings == null) throw new ArgumentNullException(nameof(consumerSettings));

            if (!requestHeaders.TryGetHeader(ReqRespMessageHeaders.ReplyTo, out object replyTo))
            {
                throw new MessageBusException($"The header {ReqRespMessageHeaders.ReplyTo} was missing on the message");
            }

            AddMessageTypeHeader(response, responseHeaders);

            var responsePayload = response != null
                ? Settings.Serializer.Serialize(consumerSettings.ResponseType, response)
                : null;

            return ProduceToTransport(response, (string)replyTo, responsePayload, responseHeaders);
        }

        /// <summary>
        /// Should be invoked by the concrete bus implementation whenever there is a message arrived on the reply to topic.
        /// </summary>
        /// <param name="responsePayload"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public virtual Task<Exception> OnResponseArrived(byte[] responsePayload, string path, IDictionary<string, object> responseHeaders)
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

            return OnResponseArrived(responsePayload, path, requestId, responseException);
        }

        /// <summary>
        /// Should be invoked by the concrete bus implementation whenever there is a message arrived on the reply to topic name.
        /// </summary>
        /// <param name="reponse"></param>
        /// <param name="path"></param>
        /// <param name="requestId"></param>
        /// <param name="errorMessage"></param>
        /// <returns></returns>
        public virtual Task<Exception> OnResponseArrived(byte[] responsePayload, string path, string requestId, Exception responseException, object response = null)
        {
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
                    var tookTimespan = CurrentTime.Subtract(requestState.Created);
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
                        response = responsePayload != null ? Serializer.Deserialize(requestState.ResponseType, responsePayload) : response;

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

        public virtual bool IsMessageScopeEnabled(ConsumerSettings consumerSettings) => consumerSettings.IsMessageScopeEnabled ?? Settings.IsMessageScopeEnabled ?? true;

        public virtual MessageScopeWrapper GetMessageScope(ConsumerSettings consumerSettings, object message)
        {
            // ToDo: Move to MessageBusProxy to pick up the local scope
            var createMessageScope = IsMessageScopeEnabled(consumerSettings);
            return new MessageScopeWrapper(_logger, Settings.DependencyResolver, createMessageScope, message);
        }

        #region Implementation of IMessageBus

        #region Implementation of IPublishBus

        public virtual Task Publish<TMessage>(TMessage message, string path = null, IDictionary<string, object> headers = null, CancellationToken cancellationToken = default)
            => Publish(message, path, headers, cancellationToken, currentDependencyResolver: null);

        #endregion

        #region Implementation of IRequestResponseBus

        public Task<TResponseMessage> Send<TResponseMessage>(IRequestMessage<TResponseMessage> request, CancellationToken cancellationToken)
            => SendInternal<TResponseMessage>(request, timeout: null, path: null, headers: null, cancellationToken);

        public Task<TResponseMessage> Send<TResponseMessage, TRequestMessage>(TRequestMessage request, CancellationToken cancellationToken)
            => SendInternal<TResponseMessage>(request, timeout: null, path: null, headers: null, cancellationToken);

        public virtual Task<TResponseMessage> Send<TResponseMessage>(IRequestMessage<TResponseMessage> request, string path = null, IDictionary<string, object> headers = null, CancellationToken cancellationToken = default)
            => SendInternal<TResponseMessage>(request, null, path, headers, cancellationToken);

        public Task<TResponseMessage> Send<TResponseMessage, TRequestMessage>(TRequestMessage request, string path = null, IDictionary<string, object> headers = null, CancellationToken cancellationToken = default)
            => SendInternal<TResponseMessage>(request, null, path, headers, cancellationToken);

        public Task<TResponseMessage> Send<TResponseMessage>(IRequestMessage<TResponseMessage> request, TimeSpan timeout, string path = null, IDictionary<string, object> headers = null, CancellationToken cancellationToken = default)
            => SendInternal<TResponseMessage>(request, timeout, path, headers, cancellationToken);

        #endregion

        #endregion
    }
}