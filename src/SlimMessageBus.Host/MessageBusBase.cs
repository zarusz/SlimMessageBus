using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.RequestResponse;

namespace SlimMessageBus.Host
{
    public abstract class MessageBusBase : IMessageBus
    {
        private static readonly ILog Log = LogManager.GetLogger<MessageBusBase>();

        public virtual MessageBusSettings Settings { get; }

        protected IDictionary<Type, ProducerSettings> PublisherSettingsByMessageType { get; }

        protected IPendingRequestStore PendingRequestStore { get; }
        protected PendingRequestManager PendingRequestManager { get; }

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        public CancellationToken CancellationToken => _cancellationTokenSource.Token;

        protected bool IsDisposing { get; private set; }
        protected bool IsDisposed { get; private set; }

        protected MessageBusBase(MessageBusSettings settings)
        {
            AssertSettings(settings);

            Settings = settings;
            PublisherSettingsByMessageType = settings.Producers.ToDictionary(x => x.MessageType);

            PendingRequestStore = new InMemoryPendingRequestStore();
            PendingRequestManager = new PendingRequestManager(PendingRequestStore, () => CurrentTime, TimeSpan.FromSeconds(1), request =>
            {
                // Execute the event hook
                // ToDo: sort out the ConsumerSettings arg for req/resp, for now pass null
                (Settings.RequestResponse.OnMessageExpired ?? Settings.OnMessageExpired)?.Invoke(null, request);
            });
            PendingRequestManager.Start();
        }

        private static void AssertSettings(MessageBusSettings settings)
        {
            Assert.IsTrue(settings.Serializer != null, 
                () => new InvalidConfigurationMessageBusException($"{nameof(MessageBusSettings.Serializer)} was not set on {nameof(MessageBusSettings)} object"));

            Assert.IsTrue(settings.DependencyResolver != null,
                () => new InvalidConfigurationMessageBusException($"{nameof(MessageBusSettings.DependencyResolver)} was not set on {nameof(MessageBusSettings)} object"));

            if (settings.RequestResponse != null)
            {
                Assert.IsTrue(settings.RequestResponse.Topic != null,
                    () => new InvalidConfigurationMessageBusException("Request-response: topic was not set"));
            }
        }

        protected void AssertActive()
        {
            if (IsDisposed || IsDisposing)
            {
                throw new MessageBusException("The message bus is disposed at this time");
            }
        }

        protected void AssertRequestResponseConfigured()
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
            if (!PublisherSettingsByMessageType.TryGetValue(messageType, out var publisherSettings))
            {
                throw new PublishMessageBusException($"Message of type {messageType} was not registered as a supported publish message. Please check your MessageBus configuration and include this type.");
            }

            return publisherSettings;
        }

        protected virtual string GetDefaultTopic(Type messageType)
        {
            // when topic was not provided, lookup default topic from configuration
            var publisherSettings = GetProducerSettings(messageType);
            return GetDefaultTopic(messageType, publisherSettings);
        }

        protected virtual string GetDefaultTopic(Type messageType, ProducerSettings producerSettings)
        {
            var topic = producerSettings.DefaultTopic;
            if (topic == null)
            {
                throw new PublishMessageBusException($"An attempt to produce message of type {messageType} without specifying topic, but there was no default topic configured. Double check your configuration.");
            }
            Log.DebugFormat(CultureInfo.InvariantCulture, "Applying default topic {0} for message type {1}", topic, messageType);
            return topic;
        }

        public abstract Task ProduceToTransport(Type messageType, object message, string topic, byte[] payload);

        public virtual Task Publish(Type messageType, object message, string topic = null)
        {
            AssertActive();

            if (topic == null)
            {
                topic = GetDefaultTopic(messageType);
            }

            var payload = SerializeMessage(messageType, message);

            Log.DebugFormat(CultureInfo.InvariantCulture, "Producing message {0} of type {1} to topic {2} with payload size {3}", message, messageType, topic, payload?.Length ?? 0);
            return ProduceToTransport(messageType, message, topic, payload);
        }

        #region Implementation of IPublishBus

        public virtual Task Publish<TMessage>(TMessage message, string topic = null)
        {
            return Publish(message.GetType(), message, topic);
        }

        #endregion

        protected virtual TimeSpan GetDefaultRequestTimeout(Type requestType, ProducerSettings producerSettings)
        {
            var timeout = producerSettings.Timeout ?? Settings.RequestResponse.Timeout;
            Log.DebugFormat(CultureInfo.InvariantCulture, "Applying default timeout {0} for message type {1}", timeout, requestType);
            return timeout;
        }

        protected virtual async Task<TResponseMessage> SendInternal<TResponseMessage>(IRequestMessage<TResponseMessage> request, TimeSpan? timeout, string topic, CancellationToken cancellationToken)
        {
            AssertActive();
            AssertRequestResponseConfigured();

            // check if the cancellation was already requested
            cancellationToken.ThrowIfCancellationRequested();

            var requestType = request.GetType();
            var producerSettings = GetProducerSettings(requestType);

            if (topic == null)
            {
                topic = GetDefaultTopic(requestType, producerSettings);
            }

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

            if (Log.IsTraceEnabled)
            {
                Log.TraceFormat(CultureInfo.InvariantCulture, "Added to PendingRequests, total is {0}", PendingRequestStore.GetCount());
            }

            try
            {
                Log.DebugFormat(CultureInfo.InvariantCulture, "Sending request message {0} to topic {1} with reply to {2}", requestState, topic, Settings.RequestResponse.Topic);
                await ProduceRequest(request, requestMessage, topic, producerSettings).ConfigureAwait(false);
            }
            catch (PublishMessageBusException e)
            {
                Log.DebugFormat(CultureInfo.InvariantCulture, "Publishing of request message failed", e);
                // remove from registry
                PendingRequestStore.Remove(requestId);
                throw;
            }

            // convert Task<object> to Task<TResponseMessage>
            var responseUntyped = await requestState.TaskCompletionSource.Task.ConfigureAwait(true);
            return (TResponseMessage) responseUntyped;
        }

        public virtual Task ProduceRequest(object request, MessageWithHeaders requestMessage, string topic, ProducerSettings producerSettings)
        {
            var requestType = request.GetType();

            requestMessage.SetHeader(ReqRespMessageHeaders.ReplyTo, Settings.RequestResponse.Topic);
            var requestMessagePayload = SerializeRequest(requestType, request, requestMessage, producerSettings);

            return ProduceToTransport(requestType, request, topic, requestMessagePayload);
        }

        public virtual Task ProduceResponse(object request, MessageWithHeaders requestMessage, object response, MessageWithHeaders responseMessage, ConsumerSettings consumerSettings)
        {
            var replyTo = requestMessage.Headers[ReqRespMessageHeaders.ReplyTo];

            var responseMessagePayload = SerializeResponse(consumerSettings.ResponseType, response, responseMessage);

            return ProduceToTransport(consumerSettings.ResponseType, response, replyTo, responseMessagePayload);
        }

        #region Implementation of IRequestResponseBus

        public virtual Task<TResponseMessage> Send<TResponseMessage>(IRequestMessage<TResponseMessage> request, CancellationToken cancellationToken)
        {
            return SendInternal(request, null, null, cancellationToken);
        }

        public virtual Task<TResponseMessage> Send<TResponseMessage>(IRequestMessage<TResponseMessage> request, string topic = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return SendInternal(request, null, topic, cancellationToken);
        }

        public virtual Task<TResponseMessage> Send<TResponseMessage>(IRequestMessage<TResponseMessage> request, TimeSpan timeout, string topic = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return SendInternal(request, timeout, topic, cancellationToken);
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
            var responsePayload = response != null ? Settings.Serializer.Serialize(responseType, response) : null;
            // create the response wrapper message
            responseMessage.Payload = responsePayload;
            return Settings.MessageWithHeadersSerializer.Serialize(typeof(MessageWithHeaders), responseMessage);
        }

        /// <summary>
        /// Should be invoked by the concrete bus implementation whenever there is a message arrived on the reply to topic.
        /// </summary>
        /// <param name="responsePayload"></param>
        /// <param name="topic"></param>
        /// <returns></returns>
        public virtual Task OnResponseArrived(byte[] responsePayload, string topic)
        {
            var responseMessage = (MessageWithHeaders)Settings.MessageWithHeadersSerializer.Deserialize(typeof(MessageWithHeaders), responsePayload);

            if (!responseMessage.TryGetHeader(ReqRespMessageHeaders.RequestId, out string requestId))
            {
                Log.ErrorFormat(CultureInfo.InvariantCulture, "The response message arriving on topic {0} did not have the {1} header. Unable to math the response with the request. This likely indicates a misconfiguration.", topic, ReqRespMessageHeaders.RequestId);
                return Task.CompletedTask;
            }

            responseMessage.TryGetHeader(ReqRespMessageHeaders.Error, out string error);

            return OnResponseArrived(responseMessage.Payload, topic, requestId, error);
        }

        /// <summary>
        /// Should be invoked by the concrete bus implementation whenever there is a message arrived on the reply to topic.
        /// </summary>
        /// <param name="payload"></param>
        /// <param name="topic"></param>
        /// <param name="requestId"></param>
        /// <param name="errorMessage"></param>
        /// <returns></returns>
        public virtual Task OnResponseArrived(byte[] payload, string topic, string requestId, string errorMessage)
        {
            var requestState = PendingRequestStore.GetById(requestId);
            if (requestState == null)
            {
                Log.DebugFormat(CultureInfo.InvariantCulture, "The response message for request id {0} arriving on topic {1} will be disregarded. Either the request had already expired, had been cancelled or it was already handled (this response message is a duplicate).", requestId, topic);
                
                // ToDo: add and API hook to these kind of situation
                return Task.CompletedTask;
            }

            try
            {
                if (Log.IsDebugEnabled)
                {
                    var tookTimespan = CurrentTime.Subtract(requestState.Created);
                    Log.DebugFormat(CultureInfo.InvariantCulture, "Response arrived for {0} on topic {1} (time: {2} ms)", requestState, topic, tookTimespan);
                }

                if (errorMessage != null)
                {
                    // error response arrived
                    Log.DebugFormat(CultureInfo.InvariantCulture, "Response arrived for {0} on topic {1} with error: {2}", requestState, topic, errorMessage);

                    var e = new RequestHandlerFaultedMessageBusException(errorMessage);
                    requestState.TaskCompletionSource.TrySetException(e);
                }
                else
                {
                    // response arrived
                    try
                    {
                        // deserialize the response message
                        var response = Settings.Serializer.Deserialize(requestState.ResponseType, payload);

                        // resolve the response
                        requestState.TaskCompletionSource.TrySetResult(response);
                    }
                    catch (Exception e)
                    {
                        Log.DebugFormat(CultureInfo.InvariantCulture, "Could not deserialize the response message for {0} arriving on topic {1}: {2}", requestState, topic, e);
                        requestState.TaskCompletionSource.TrySetException(e);
                    }
                }
            }
            finally
            {
                // remove the request from the queue
                PendingRequestStore.Remove(requestId);
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Generates unique request IDs
        /// </summary>
        /// <returns></returns>
        protected virtual string GenerateRequestId()
        {
            return Guid.NewGuid().ToString("N");
        }
    }
}