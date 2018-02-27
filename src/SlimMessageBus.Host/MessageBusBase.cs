using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

        protected readonly IDictionary<Type, PublisherSettings> PublisherSettingsByMessageType;

        protected readonly IPendingRequestStore PendingRequestStore;
        protected readonly PendingRequestManager PendingRequestManager;

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        public CancellationToken CancellationToken => _cancellationTokenSource.Token;

        protected bool IsDisposing = false;
        protected bool IsDisposed = false;

        protected MessageBusBase(MessageBusSettings settings)
        {
            AssertSettings(settings);

            Settings = settings;
            PublisherSettingsByMessageType = settings.Publishers.ToDictionary(x => x.MessageType);

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
                    () => new InvalidConfigurationMessageBusException($"Request-response: topic was not set"));
            }
        }

        protected void AssertActive()
        {
            Assert.IsFalse(IsDisposed, () => new MessageBusException("The message bus is disposed at this time"));
        }

        #region Implementation of IDisposable

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }
            IsDisposing = true;
            try
            {
                OnDispose();
            }
            finally
            {
                IsDisposing = false;
                IsDisposed = true;
            }
        }

        protected virtual void OnDispose()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();

            PendingRequestManager.Dispose();
        }

        #endregion

        public virtual DateTimeOffset CurrentTime => DateTimeOffset.UtcNow;

        protected PublisherSettings GetPublisherSettings(Type messageType)
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
            var publisherSettings = GetPublisherSettings(messageType);
            return GetDefaultTopic(messageType, publisherSettings);
        }

        protected virtual string GetDefaultTopic(Type messageType, PublisherSettings publisherSettings)
        {
            var topic = publisherSettings.DefaultTopic;
            if (topic == null)
            {
                throw new PublishMessageBusException($"An attempt to produce message of type {messageType} without specifying topic, but there was no default topic configured. Double check your configuration.");
            }
            Log.DebugFormat("Applying default topic {0} for message type {1}", topic, messageType);
            return topic;
        }

        public abstract Task PublishToTransport(Type messageType, object message, string topic, byte[] payload);

        public virtual async Task Publish(Type messageType, object message, string topic = null)
        {
            if (topic == null)
            {
                topic = GetDefaultTopic(messageType);
            }
            var payload = Settings.Serializer.Serialize(messageType, message);

            Log.DebugFormat("Publishing message of type {0} to topic {1} with payload size {2}", messageType, topic, payload.Length);
            await PublishToTransport(messageType, message, topic, payload);
        }

        #region Implementation of IPublishBus

        public virtual async Task Publish<TMessage>(TMessage message, string topic = null)
        {
            await Publish(message.GetType(), message, topic);
        }

        #endregion

        protected virtual TimeSpan GetDefaultRequestTimeout(Type requestType, PublisherSettings publisherSettings)
        {
            var timeout = publisherSettings.Timeout ?? Settings.RequestResponse.Timeout;
            Log.DebugFormat("Applying default timeout {0} for message type {1}", timeout, requestType);
            return timeout;
        }

        protected virtual async Task<TResponseMessage> SendInternal<TResponseMessage>(IRequestMessage<TResponseMessage> request, TimeSpan? timeout, string topic, CancellationToken cancellationToken)
        {
            if (Settings.RequestResponse == null)
            {
                throw new PublishMessageBusException("An attempt to send request when request/response communication was not configured for the message bus. Ensure you configure the bus properly before the application starts.");
            }

            // check if the cancellation was already requested
            cancellationToken.ThrowIfCancellationRequested();

            var requestType = request.GetType();
            var publisherSettings = GetPublisherSettings(requestType);

            if (topic == null)
            {
                topic = GetDefaultTopic(requestType, publisherSettings);
            }

            if (timeout == null)
            {
                timeout = GetDefaultRequestTimeout(requestType, publisherSettings);
            }

            var replyTo = Settings.RequestResponse.Topic;
            var created = CurrentTime;
            var expires = created.Add(timeout.Value);

            // generate the request guid
            var requestId = GenerateRequestId();
            var requestPayload = SerializeRequest(requestType, request, requestId, replyTo, expires);

            // record the request state
            var requestState = new PendingRequestState(requestId, request, requestType, typeof(TResponseMessage), created, expires, cancellationToken);
            PendingRequestStore.Add(requestState);

            if (Log.IsTraceEnabled)
                Log.TraceFormat("Added to PendingRequests, total is {0}", PendingRequestStore.GetCount());

            try
            {
                Log.DebugFormat("Sending request message {0} to topic {1} with reply to {2} and payload size {3}", requestState, topic, replyTo, requestPayload.Length);
                await PublishToTransport(requestType, request, topic, requestPayload);
            }
            catch (PublishMessageBusException e)
            {
                Log.DebugFormat("Publishing of request message failed: {0}", e);
                // remove from registry
                PendingRequestStore.Remove(requestId);
                throw;
            }

            // convert Task<object> to Task<TResponseMessage>
            var typedTask = Convert<TResponseMessage>(requestState.TaskCompletionSource.Task);
            return await typedTask;
        }

        #region Implementation of IRequestResponseBus

        public virtual async Task<TResponseMessage> Send<TResponseMessage>(IRequestMessage<TResponseMessage> request, CancellationToken cancellationToken)
        {
            return await SendInternal(request, null, null, cancellationToken);
        }

        public virtual async Task<TResponseMessage> Send<TResponseMessage>(IRequestMessage<TResponseMessage> request, string topic = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await SendInternal(request, null, topic, cancellationToken);
        }

        public virtual async Task<TResponseMessage> Send<TResponseMessage>(IRequestMessage<TResponseMessage> request, TimeSpan timeout, string topic = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await SendInternal(request, (TimeSpan?) timeout, topic, cancellationToken);
        }

        #endregion

        public virtual byte[] SerializeRequest(Type requestType, object request, string requestId, string replyTo, DateTimeOffset? expires)
        {
            var requestPayload = Settings.Serializer.Serialize(requestType, request);

            // create the request wrapper message
            var requestMessage = new MessageWithHeaders(requestPayload);
            requestMessage.SetHeader(ReqRespMessageHeaders.RequestId, requestId);
            requestMessage.SetHeader(ReqRespMessageHeaders.ReplyTo, replyTo);
            if (expires.HasValue)
            {
                requestMessage.SetHeader(ReqRespMessageHeaders.Expires, expires.Value);
            }

            var requestMessagePayload = Settings.MessageWithHeadersSerializer.Serialize(typeof(MessageWithHeaders), requestMessage);
            return requestMessagePayload;
        }

        public virtual object DeserializeRequest(Type requestType, byte[] requestPayload, out string requestId, out string replyTo, out DateTimeOffset? expires)
        {
            var requestMessage = (MessageWithHeaders)Settings.MessageWithHeadersSerializer.Deserialize(typeof(MessageWithHeaders), requestPayload);
            requestMessage.TryGetHeader(ReqRespMessageHeaders.RequestId, out requestId);
            requestMessage.TryGetHeader(ReqRespMessageHeaders.ReplyTo, out replyTo);
            requestMessage.TryGetHeader(ReqRespMessageHeaders.Expires, out expires);

            var request = Settings.Serializer.Deserialize(requestType, requestMessage.Payload);
            return request;
        }

        public virtual byte[] SerializeResponse(Type responseType, object response, string requestId, string error)
        {
            var responsePayload = error == null ? Settings.Serializer.Serialize(responseType, response) : null;

            // create the response wrapper message
            var responseMessage = new MessageWithHeaders(responsePayload);
            responseMessage.SetHeader(ReqRespMessageHeaders.RequestId, requestId);
            if (error != null)
            {
                responseMessage.SetHeader(ReqRespMessageHeaders.Error, error);
            }

            var responseMessagePayload = Settings.MessageWithHeadersSerializer.Serialize(typeof(MessageWithHeaders), responseMessage);
            return responseMessagePayload;
        }

        /// <summary>
        /// Should be invoked by the concrete bus implementation whenever there is a message arrived on the reply to topic.
        /// </summary>
        /// <param name="responsePayload"></param>
        /// <param name="topic"></param>
        /// <returns></returns>
        public virtual async Task OnResponseArrived(byte[] responsePayload, string topic)
        {
            var responseMessage = (MessageWithHeaders)Settings.MessageWithHeadersSerializer.Deserialize(typeof(MessageWithHeaders), responsePayload);

            if (!responseMessage.TryGetHeader(ReqRespMessageHeaders.RequestId, out string requestId))
            {
                Log.ErrorFormat("The response message arriving on topic {0} did not have the {1} header. Unable to math the response with the request. This likely indicates a misconfiguration.", topic, ReqRespMessageHeaders.RequestId);
                return;
            }

            responseMessage.TryGetHeader(ReqRespMessageHeaders.Error, out string error);

            await OnResponseArrived(responseMessage.Payload, topic, requestId, error);
        }

        /// <summary>
        /// Should be invoked by the concrete bus implementation whenever there is a message arrived on the reply to topic.
        /// </summary>
        /// <param name="payload"></param>
        /// <param name="topic"></param>
        /// <param name="requestId"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        public virtual async Task OnResponseArrived(byte[] payload, string topic, string requestId, string error)
        {
            var requestState = PendingRequestStore.GetById(requestId);
            if (requestState == null)
            {
                Log.DebugFormat("The response message for request id {0} arriving on topic {1} will be disregarded. Either the request had already expired, had been cancelled or it was already handled (this response message is a duplicate).", requestId, topic);
                
                // ToDo: add and API hook to these kind of situation
                return;
            }

            try
            {
                if (Log.IsDebugEnabled)
                {
                    var tookTimespan = CurrentTime.Subtract(requestState.Created);
                    Log.DebugFormat("Response arrived for {0} on topic {1} (time: {2} ms)", requestState, topic, tookTimespan);
                }

                if (error != null)
                {
                    // error response arrived
                    Log.DebugFormat("Response arrived for {0} on topic {1} with error: {2}", requestState, topic, error);

                    var e = new RequestHandlerFaultedMessageBusException(error);
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
                        Log.DebugFormat("Could not deserialize the response message for {0} arriving on topic {1}: {2}", requestState, topic, e);
                        requestState.TaskCompletionSource.TrySetException(e);
                    }
                }
            }
            finally
            {
                // remove the request from the queue
                PendingRequestStore.Remove(requestId);
            }
        }

        /// <summary>
        /// Gnenerates unique request IDs
        /// </summary>
        /// <returns></returns>
        protected virtual string GenerateRequestId()
        {
            return Guid.NewGuid().ToString("N");
        }

        private static async Task<T> Convert<T>(Task<object> task)
        {
            var result = await task;
            return (T)result;
        }
    }
}