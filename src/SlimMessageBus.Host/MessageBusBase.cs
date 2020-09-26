using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SlimMessageBus.Host.Collections;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host
{
    public abstract class MessageBusBase : IMessageBus
    {
        private readonly ILogger _logger;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly SafeDictionaryWrapper<Type, Type> _messageTypeToBaseType = new SafeDictionaryWrapper<Type, Type>();

        public ILoggerFactory LoggerFactory { get; }

        public virtual MessageBusSettings Settings { get; }

        protected IDictionary<Type, ProducerSettings> ProducerSettingsByMessageType { get; private set; }
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
        }

        /// <summary>
        /// Called by the provider to initialize the bus.
        /// </summary>
        protected void OnBuildProvider()
        {
            AssertSettings();
            Build();
        }

        protected virtual void Build()
        {
            ProducerSettingsByMessageType = new Dictionary<Type, ProducerSettings>();
            foreach (var producerSettings in Settings.Producers)
            {
                if (ProducerSettingsByMessageType.ContainsKey(producerSettings.MessageType))
                {
                    throw new ConfigurationMessageBusException($"The produced message type '{producerSettings.MessageType}' was declared more than once (check the {nameof(MessageBusBuilder.Produce)} configuration)");
                }
                ProducerSettingsByMessageType.Add(producerSettings.MessageType, producerSettings);
            }

            PendingRequestStore = new InMemoryPendingRequestStore();
            PendingRequestManager = new PendingRequestManager(PendingRequestStore, () => CurrentTime, TimeSpan.FromSeconds(1), LoggerFactory, request =>
            {
                // Execute the event hook
                try
                {
                    (Settings.RequestResponse.OnMessageExpired ?? Settings.OnMessageExpired)?.Invoke(this, Settings.RequestResponse, request);
                }
                catch (Exception eh)
                {
                    HookFailed(_logger, eh, nameof(IConsumerEvents.OnMessageExpired));
                }
            });
            PendingRequestManager.Start();
        }

        public static void HookFailed(ILogger logger, Exception eh, string name)
        {
            // When the hook itself error out, catch the exception
            logger.LogError(eh, "{0} method failed", name);
        }

        protected virtual void AssertSettings()
        {
            foreach (var consumerSettings in Settings.Consumers)
            {
                AssertConsumerSettings(consumerSettings);
            }
            AssertSerializerSettings();
            AssertDepencendyResolverSettings();
            AssertRequestResponseSettings();
        }

        protected virtual void AssertConsumerSettings(ConsumerSettings consumerSettings)
        {
            if (consumerSettings == null) throw new ArgumentNullException(nameof(consumerSettings));

            Assert.IsNotNull(consumerSettings.Topic,
                () => new ConfigurationMessageBusException($"The {nameof(ConsumerSettings)}.{nameof(consumerSettings.Topic)} is not set"));
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
                Assert.IsNotNull(Settings.RequestResponse.Topic,
                    () => new ConfigurationMessageBusException("Request-response: name was not set"));
            }
        }

        protected void AssertActive()
        {
            if (IsDisposed || IsDisposing)
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

        #region Implementation of IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (IsDisposed)
            {
                return;
            }
            IsDisposing = true;
            try
            {
                if (disposing)
                {
                    _cancellationTokenSource.Cancel();
                    _cancellationTokenSource.Dispose();

                    PendingRequestManager.Dispose();
                }
            }
            finally
            {
                IsDisposing = false;
                IsDisposed = true;
            }
        }

        #endregion

        public virtual DateTimeOffset CurrentTime => DateTimeOffset.UtcNow;

        protected ProducerSettings GetProducerSettings(Type messageType)
        {
            if (!ProducerSettingsByMessageType.TryGetValue(messageType, out var producerSettings))
            {
                var baseMessageType = _messageTypeToBaseType.GetOrAdd(messageType, mt =>
                {
                    var baseType = mt;
                    do
                    {
                        baseType = mt.BaseType;
                    }
                    while (baseType != null && baseType != typeof(object) && !ProducerSettingsByMessageType.ContainsKey(baseType));

                    if (baseType != null)
                    {
                        _logger.LogDebug("Found a base type of {0} that is configured in the bus: {1}", mt, baseType);
                    }
                    else
                    {
                        _logger.LogDebug("Did not find any base type of {0} that is configured in the bus", mt);
                    }

                    // Note: Nulls are also added to dictionary, so that we don't look them up using reflection next time (cached).
                    return baseType;
                });

                if (baseMessageType == null)
                {
                    throw new PublishMessageBusException($"Message of type {messageType} was not registered as a supported publish message. Please check your MessageBus configuration and include this type.");
                }

                producerSettings = ProducerSettingsByMessageType[baseMessageType];
            }

            return producerSettings;
        }

        protected virtual string GetDefaultName(Type messageType)
        {
            // when topic was not provided, lookup default topic from configuration
            var producerSettings = GetProducerSettings(messageType);
            return GetDefaultName(messageType, producerSettings);
        }

        protected virtual string GetDefaultName(Type messageType, ProducerSettings producerSettings)
        {
            if (producerSettings == null) throw new ArgumentNullException(nameof(producerSettings));

            var name = producerSettings.DefaultTopic;
            if (name == null)
            {
                throw new PublishMessageBusException($"An attempt to produce message of type {messageType} without specifying name, but there was no default name configured. Double check your configuration.");
            }

            _logger.LogDebug("Applying default name {0} for message type {1}", name, messageType);
            return name;
        }

        public abstract Task ProduceToTransport(Type messageType, object message, string name, byte[] messagePayload, MessageWithHeaders messageWithHeaders = null);

        public virtual Task Publish(Type messageType, object message, string name = null)
        {
            AssertActive();

            var producerSettings = GetProducerSettings(messageType);

            if (name == null)
            {
                name = GetDefaultName(producerSettings.MessageType, producerSettings);
            }

            OnProducedHook(message, name, producerSettings);

            var payload = SerializeMessage(producerSettings.MessageType, message);

            _logger.LogDebug("Producing message {0} of type {1} to name {2} with payload size {3}", message, producerSettings.MessageType, name, payload?.Length ?? 0);
            return ProduceToTransport(producerSettings.MessageType, message, name, payload);
        }

        #region Implementation of IPublishBus

        public virtual Task Publish<TMessage>(TMessage message, string name = null)
        {
            return Publish(typeof(TMessage), message, name);
        }

        #endregion

        protected virtual TimeSpan GetDefaultRequestTimeout(Type requestType, ProducerSettings producerSettings)
        {
            if (producerSettings == null) throw new ArgumentNullException(nameof(producerSettings));

            var timeout = producerSettings.Timeout ?? Settings.RequestResponse.Timeout;
            _logger.LogDebug("Applying default timeout {0} for message type {1}", timeout, requestType);
            return timeout;
        }

        protected virtual async Task<TResponseMessage> SendInternal<TResponseMessage>(IRequestMessage<TResponseMessage> request, TimeSpan? timeout, string name, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            AssertActive();
            AssertRequestResponseConfigured();

            // check if the cancellation was already requested
            cancellationToken.ThrowIfCancellationRequested();

            var requestType = request.GetType();
            var producerSettings = GetProducerSettings(requestType);

            if (name == null)
            {
                name = GetDefaultName(requestType, producerSettings);
            }

            OnProducedHook(request, name, producerSettings);

            if (timeout == null)
            {
                timeout = GetDefaultRequestTimeout(requestType, producerSettings);
            }

            var created = CurrentTime;
            var expires = created.Add(timeout.Value);

            // generate the request guid
            var requestId = GenerateRequestId();
            var requestMessage = new MessageWithHeaders();
            requestMessage.SetHeader(ReqRespMessageHeaders.RequestId, requestId);
            requestMessage.SetHeader(ReqRespMessageHeaders.Expires, expires);

            // record the request state
            var requestState = new PendingRequestState(requestId, request, requestType, typeof(TResponseMessage), created, expires, cancellationToken);
            PendingRequestStore.Add(requestState);

            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Added to PendingRequests, total is {0}", PendingRequestStore.GetCount());
            }

            try
            {
                _logger.LogDebug("Sending request message {0} to name {1} with reply to {2}", requestState, name, Settings.RequestResponse.Topic);
                await ProduceRequest(request, requestMessage, name, producerSettings).ConfigureAwait(false);
            }
            catch (PublishMessageBusException e)
            {
                _logger.LogDebug("Publishing of request message failed", e);
                // remove from registry
                PendingRequestStore.Remove(requestId);
                throw;
            }

            // convert Task<object> to Task<TResponseMessage>
            var responseUntyped = await requestState.TaskCompletionSource.Task.ConfigureAwait(true);
            return (TResponseMessage)responseUntyped;
        }

        private void OnProducedHook(object message, string name, ProducerSettings producerSettings)
        {
            try
            {
                producerSettings.OnMessageProduced?.Invoke(this, producerSettings, message, name);
                Settings.OnMessageProduced?.Invoke(this, producerSettings, message, name);
            }
            catch (Exception eh)
            {
                HookFailed(_logger, eh, nameof(IProducerEvents.OnMessageProduced));
            }
        }

        public virtual Task ProduceRequest(object request, MessageWithHeaders requestMessage, string name, ProducerSettings producerSettings)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (requestMessage == null) throw new ArgumentNullException(nameof(requestMessage));

            var requestType = request.GetType();

            requestMessage.SetHeader(ReqRespMessageHeaders.ReplyTo, Settings.RequestResponse.Topic);
            var requestMessagePayload = SerializeRequest(requestType, request, requestMessage, producerSettings);

            return ProduceToTransport(requestType, request, name, requestMessagePayload, requestMessage);
        }

        public virtual Task ProduceResponse(object request, MessageWithHeaders requestMessage, object response, MessageWithHeaders responseMessage, ConsumerSettings consumerSettings)
        {
            if (requestMessage == null) throw new ArgumentNullException(nameof(requestMessage));
            if (consumerSettings == null) throw new ArgumentNullException(nameof(consumerSettings));

            var replyTo = requestMessage.Headers[ReqRespMessageHeaders.ReplyTo];

            var responseMessagePayload = SerializeResponse(consumerSettings.ResponseType, response, responseMessage);

            return ProduceToTransport(consumerSettings.ResponseType, response, replyTo, responseMessagePayload);
        }

        #region Implementation of IRequestResponseBus

        public virtual Task<TResponseMessage> Send<TResponseMessage>(IRequestMessage<TResponseMessage> request, CancellationToken cancellationToken)
        {
            return SendInternal(request, null, null, cancellationToken);
        }

        public virtual Task<TResponseMessage> Send<TResponseMessage>(IRequestMessage<TResponseMessage> request, string name = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return SendInternal(request, null, name, cancellationToken);
        }

        public virtual Task<TResponseMessage> Send<TResponseMessage>(IRequestMessage<TResponseMessage> request, TimeSpan timeout, string name = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return SendInternal(request, timeout, name, cancellationToken);
        }

        #endregion

        public virtual byte[] SerializeMessage(Type messageType, object message)
        {
            return Settings.Serializer.Serialize(messageType, message);
        }

        public virtual object DeserializeMessage(Type messageType, byte[] payload)
        {
            return Settings.Serializer.Deserialize(messageType, payload);
        }

        public virtual byte[] SerializeRequest(Type requestType, object request, MessageWithHeaders requestMessage, ProducerSettings producerSettings)
        {
            if (requestMessage == null) throw new ArgumentNullException(nameof(requestMessage));

            var requestPayload = SerializeMessage(requestType, request);
            // create the request wrapper message
            requestMessage.Payload = requestPayload;
            return Settings.MessageWithHeadersSerializer.Serialize(typeof(MessageWithHeaders), requestMessage);
        }

        public virtual object DeserializeRequest(Type requestType, byte[] requestMessagePayload, out MessageWithHeaders requestMessage)
        {
            requestMessage = (MessageWithHeaders)Settings.MessageWithHeadersSerializer.Deserialize(typeof(MessageWithHeaders), requestMessagePayload);
            return DeserializeMessage(requestType, requestMessage.Payload);
        }

        public virtual byte[] SerializeResponse(Type responseType, object response, MessageWithHeaders responseMessage)
        {
            if (responseMessage == null) throw new ArgumentNullException(nameof(responseMessage));

            var responsePayload = response != null ? Settings.Serializer.Serialize(responseType, response) : null;
            // create the response wrapper message
            responseMessage.Payload = responsePayload;
            return Settings.MessageWithHeadersSerializer.Serialize(typeof(MessageWithHeaders), responseMessage);
        }

        public virtual object DeserializeResponse(Type responseType, byte[] responsePayload)
        {
            return Settings.Serializer.Deserialize(responseType, responsePayload);
        }

        /// <summary>
        /// Should be invoked by the concrete bus implementation whenever there is a message arrived on the reply to topic.
        /// </summary>
        /// <param name="responsePayload"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public virtual Task<Exception> OnResponseArrived(byte[] responsePayload, string name)
        {
            var responseMessage = (MessageWithHeaders)Settings.MessageWithHeadersSerializer.Deserialize(typeof(MessageWithHeaders), responsePayload);

            if (!responseMessage.TryGetHeader(ReqRespMessageHeaders.RequestId, out string requestId))
            {
                _logger.LogError("The response message arriving on name {0} did not have the {1} header. Unable to math the response with the request. This likely indicates a misconfiguration.", name, ReqRespMessageHeaders.RequestId);
                return Task.FromResult<Exception>(null);
            }

            responseMessage.TryGetHeader(ReqRespMessageHeaders.Error, out string error);

            return OnResponseArrived(responseMessage.Payload, name, requestId, error);
        }

        /// <summary>
        /// Should be invoked by the concrete bus implementation whenever there is a message arrived on the reply to topic name.
        /// </summary>
        /// <param name="reponse"></param>
        /// <param name="name"></param>
        /// <param name="requestId"></param>
        /// <param name="errorMessage"></param>
        /// <returns></returns>
        public virtual Task<Exception> OnResponseArrived(byte[] responsePayload, string name, string requestId, string errorMessage, object response = null)
        {
            var requestState = PendingRequestStore.GetById(requestId);
            if (requestState == null)
            {
                _logger.LogDebug("The response message for request id {0} arriving on name {1} will be disregarded. Either the request had already expired, had been cancelled or it was already handled (this response message is a duplicate).", requestId, name);

                // ToDo: add and API hook to these kind of situation
                return Task.FromResult<Exception>(null);
            }

            try
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    var tookTimespan = CurrentTime.Subtract(requestState.Created);
                    _logger.LogDebug("Response arrived for {0} on name {1} (time: {2} ms)", requestState, name, tookTimespan);
                }

                if (errorMessage != null)
                {
                    // error response arrived
                    _logger.LogDebug("Response arrived for {0} on name {1} with error: {2}", requestState, name, errorMessage);

                    var e = new RequestHandlerFaultedMessageBusException(errorMessage);
                    requestState.TaskCompletionSource.TrySetException(e);
                }
                else
                {
                    // response arrived
                    try
                    {
                        // deserialize the response message
                        response = responsePayload != null ? DeserializeResponse(requestState.ResponseType, responsePayload) : response;

                        // resolve the response
                        requestState.TaskCompletionSource.TrySetResult(response);
                    }
                    catch (Exception e)
                    {
                        _logger.LogDebug("Could not deserialize the response message for {0} arriving on name {1}: {2}", requestState, name, e);
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
    }
}