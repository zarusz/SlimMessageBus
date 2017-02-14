using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Common.Logging;
using SlimMessageBus.Host.Config;
using Timer = System.Timers.Timer;

namespace SlimMessageBus.Host
{
    public abstract class MessageBusBase : IMessageBus
    {
        private static readonly ILog Log = LogManager.GetLogger<MessageBusBase>();

        public MessageBusSettings Settings { get; }

        protected readonly IDictionary<Type, PublisherSettings> PublisherSettingsByMessageType;

        protected readonly ConcurrentDictionary<string, PendingRequestState> PendingRequests;
        protected readonly Timer PendingRequestsTimer;

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        public CancellationToken CancellationToken => _cancellationTokenSource.Token;

        protected MessageBusBase(MessageBusSettings settings)
        {
            AssertSettings(settings);

            Settings = settings;
            PublisherSettingsByMessageType = Settings.Publishers.ToDictionary(x => x.MessageType);

            PendingRequests = new ConcurrentDictionary<string, PendingRequestState>();

            PendingRequestsTimer = new Timer
            {
                Interval = 1000,
                AutoReset = true
            };
            PendingRequestsTimer.Elapsed += CleanRequestReqistry;
            PendingRequestsTimer.Start();
        }

        private static void AssertSettings(MessageBusSettings settings)
        {
            Assert.IsTrue(settings.Serializer != null, 
                () => new InvalidConfigurationMessageBusException($"Serializer was not provided"));

            if (settings.RequestResponse != null)
            {
                Assert.IsTrue(settings.RequestResponse.Topic != null,
                    () => new InvalidConfigurationMessageBusException($"Request-response: topic was not provided"));
            }
        }

        #region Implementation of IDisposable

        public virtual void Dispose()
        {
            PendingRequestsTimer.Stop();
            PendingRequestsTimer.Dispose();
        }

        #endregion

        protected virtual void CleanRequestReqistry(object sender, ElapsedEventArgs args)
        {
            var now = CurrentTime;

            var requestsToExpire = PendingRequests.Values.Where(x => x.Expires < now).ToList();
            foreach (var requestState in requestsToExpire)
            {
                var canceled = requestState.TaskCompletionSource.TrySetCanceled();

                PendingRequestState s;
                if (PendingRequests.TryRemove(requestState.Id, out s) && canceled)
                {
                    Log.DebugFormat("Pending request timed-out: {0}, now: {1}", requestState, now);
                    // ToDo: add and API hook to these kind of situation
                }
            }
        }

        public virtual DateTimeOffset CurrentTime => DateTimeOffset.UtcNow;

        protected PublisherSettings GetPublisherSettings(Type messageType)
        {
            PublisherSettings publisherSettings;
            if (!PublisherSettingsByMessageType.TryGetValue(messageType, out publisherSettings))
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

        public abstract Task Publish(Type messageType, byte[] payload, string topic);

        public virtual async Task Publish(Type messageType, object message, string topic = null)
        {
            if (topic == null)
            {
                topic = GetDefaultTopic(messageType);
            }
            var payload = Settings.Serializer.Serialize(messageType, message);

            Log.DebugFormat("Publishing message of type {0} to topic {1} with payload size {2}", messageType, topic, payload.Length);
            await Publish(messageType, payload, topic);
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

        public virtual async Task<TResponseMessage> Send<TResponseMessage>(IRequestMessage<TResponseMessage> request, TimeSpan? timeout, string topic)
        {
            if (Settings.RequestResponse == null)
            {
                throw new PublishMessageBusException("An attempt to send request when request/response communication was not configured for the message bus. Ensure you configure the bus properly before the application starts.");
            }

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
            var requestState = new PendingRequestState(requestId, request, requestType, typeof(TResponseMessage), created, expires);
            PendingRequests.TryAdd(requestId, requestState);
            Log.DebugFormat("Added to PendingRequests, total is {0}", PendingRequests.Count);

            try
            {
                Log.DebugFormat("Sending request message {0} to topic {1} with reply to {2} and payload size {3}", requestState, topic, replyTo, requestPayload.Length);
                await Publish(requestType, requestPayload, topic);
            }
            catch (PublishMessageBusException e)
            {
                Log.DebugFormat("Publishing of request message failed: {0}", e);
                // remove from registry
                PendingRequests.TryRemove(requestId, out requestState);
                throw;
            }

            // convert Task<object> to Task<TResponseMessage>
            var typedTask = Convert<TResponseMessage>(requestState.TaskCompletionSource.Task);
            return await typedTask;
        }

        #region Implementation of IRequestResponseBus

        public virtual async Task<TResponseMessage> Send<TResponseMessage>(IRequestMessage<TResponseMessage> request, string topic = null)
        {
            return await Send(request, null, topic);
        }

        public virtual async Task<TResponseMessage> Send<TResponseMessage>(IRequestMessage<TResponseMessage> request, TimeSpan timeout, string topic = null)
        {
            return await Send(request, (TimeSpan?) timeout, topic);
        }

        #endregion

        public virtual byte[] SerializeRequest(Type requestType, object request, string requestId, string replyTo, DateTimeOffset? expires)
        {
            var requestPayload = Settings.Serializer.Serialize(requestType, request);

            // create the request wrapper message
            var requestMessage = new MessageWithHeaders(requestPayload);
            requestMessage.Headers.Add(MessageHeaders.RequestId, requestId);
            requestMessage.Headers.Add(MessageHeaders.ReplyTo, replyTo);
            if (expires.HasValue)
            {
                requestMessage.Headers.Add(MessageHeaders.Expires, expires.Value.ToUnixTimeSeconds().ToString());
            }

            var requestMessagePayload = Settings.MessageWithHeadersSerializer.Serialize(typeof(MessageWithHeaders), requestMessage);
            return requestMessagePayload;
        }

        public virtual object DeserializeRequest(Type requestType, byte[] requestPayload, out string requestId, out string replyTo, out DateTimeOffset? expires)
        {
            var requestMessage = (MessageWithHeaders)Settings.MessageWithHeadersSerializer.Deserialize(typeof(MessageWithHeaders), requestPayload);
            requestId = requestMessage.Headers[MessageHeaders.RequestId];
            replyTo = requestMessage.Headers[MessageHeaders.ReplyTo];

            string expiresStr;
            if (requestMessage.Headers.TryGetValue(MessageHeaders.Expires, out expiresStr))
            {
                expires = DateTimeOffset.FromUnixTimeSeconds(long.Parse(expiresStr));
            }
            else
            {
                expires = null;
            }

            var request = Settings.Serializer.Deserialize(requestType, requestMessage.Payload);
            return request;
        }

        public virtual byte[] SerializeResponse(Type responseType, object response, string requestId)
        {
            var responsePayload = Settings.Serializer.Serialize(responseType, response);

            // create the response wrapper message
            var responseMessage = new MessageWithHeaders(responsePayload);
            responseMessage.Headers.Add(MessageHeaders.RequestId, requestId);

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
            string requestId;
            var responseMessage = (MessageWithHeaders)Settings.MessageWithHeadersSerializer.Deserialize(typeof(MessageWithHeaders), responsePayload);
            if (!responseMessage.Headers.TryGetValue(MessageHeaders.RequestId, out requestId))
            {
                Log.ErrorFormat("The response message arriving on topic {0} did not have the {1} header. Unable to math the response with the request. This likely indicates a misconfiguration.", topic, MessageHeaders.RequestId);
                return;
            }

            await OnResponseArrived(responseMessage.Payload, topic, requestId);
        }

        /// <summary>
        /// Should be invoked by the concrete bus implementation whenever there is a message arrived on the reply to topic.
        /// </summary>
        /// <param name="payload"></param>
        /// <param name="topic"></param>
        /// <param name="requestId"></param>
        /// <returns></returns>
        public virtual async Task OnResponseArrived(byte[] payload, string topic, string requestId)
        {
            PendingRequestState requestState;

            if (!PendingRequests.TryGetValue(requestId, out requestState))
            {
                Log.DebugFormat("The response message with request id {0} arriving on topic {1} already expired.", requestId, topic);
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

                object response = null;
                try
                {
                    response = Settings.Serializer.Deserialize(requestState.ResponseType, payload);
                }
                catch (Exception e)
                {
                    Log.DebugFormat("Could not deserialize the response message for {0} arriving on topic {1}: {2}", requestState, topic, e);
                    requestState.TaskCompletionSource.SetException(e);
                }

                if (response != null)
                {
                    // resolve the task
                    requestState.TaskCompletionSource.SetResult(response);
                }
            }
            finally
            {
                // remove the request from the queue
                PendingRequests.TryRemove(requestId, out requestState);
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