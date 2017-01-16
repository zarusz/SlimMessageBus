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
    public abstract class MessageBusBus : IMessageBus
    {
        private static readonly ILog Log = LogManager.GetLogger<MessageBusBus>();

        public const string HeaderRequestId = "request-id";
        public const string HeaderReplyTo = "reply-to";

        public MessageBusSettings Settings { get; }

        protected readonly IDictionary<Type, PublisherSettings> PublisherSettingsByMessageType;

        protected readonly ConcurrentDictionary<string, PendingRequestState> PendingRequests;
        protected readonly Timer PendingRequestsTimer;

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        public CancellationToken CancellationToken => _cancellationTokenSource.Token;
        
        protected MessageBusBus(MessageBusSettings settings)
        {
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

        protected virtual void CleanRequestReqistry(object sender, ElapsedEventArgs args)
        {
            var now = DateTime.Now;

            var requestsToExpire = PendingRequests.Values.Where(x => x.Expire < now).ToList();
            foreach (var requestState in requestsToExpire)
            {
                var canceled = requestState.TaskCompletionSource.TrySetCanceled();
                if (canceled)
                {
                    Log.DebugFormat("Pending request timed-out: {0}", requestState);
                }

                PendingRequestState s;
                PendingRequests.TryRemove(requestState.Id, out s);
            }
        }

        #region Implementation of IDisposable

        public virtual void Dispose()
        {
            PendingRequestsTimer.Stop();
            PendingRequestsTimer.Dispose();
        }

        #endregion

        protected virtual string GetDefaultTopic(Type messageType)
        {
            // when topic was not provided, lookup default topic from configuration

            PublisherSettings publisherSettings;
            if (!PublisherSettingsByMessageType.TryGetValue(messageType, out publisherSettings))
            {
                throw new PublishMessageBusException($"Message of type {messageType} was not registered as a supported publish message. Please check your MessageBus configuration and include this type.");
            }

            var topic = publisherSettings.DefaultTopic;
            Log.DebugFormat("Applying default topic {0} for message type {1}", topic, messageType);
            return topic;
        }

        protected abstract Task Publish(Type type, string topic, byte[] payload);

        #region Implementation of IPublishBus

        public virtual async Task Publish<TMessage>(TMessage message, string topic = null)
        {
            var messageType = message.GetType();
            if (topic == null)
            {
                topic = GetDefaultTopic(messageType);
            }
            var payload = Settings.Serializer.Serialize(messageType, message);

            Log.DebugFormat("Publishing message of type {0} to topic {1} with payload size {2}", messageType, topic, payload.Length);
            await Publish(messageType, topic, payload);
        }

        #endregion

        #region Implementation of IRequestResponseBus

        public virtual async Task<TResponseMessage> Send<TResponseMessage>(IRequestMessage<TResponseMessage> request)
        {
            return await Send(request, Settings.RequestResponse.Timeout);
        }

        public virtual async Task<TResponseMessage> Send<TResponseMessage>(IRequestMessage<TResponseMessage> request, TimeSpan timeout)
        {
            if (Settings.RequestResponse == null)
            {
                throw new PublishMessageBusException("An attempt to send ruequest while request/response communication was not configured for the message bus. Ensure you configure the bus properly before the application starts.");
            }

            var requestType = request.GetType();
            var topic = GetDefaultTopic(requestType);
            var replyTo = Settings.RequestResponse.Topic;

            // serialize the message
            var payload = Settings.Serializer.Serialize(requestType, request);

            // generate the request guid
            var requestId = GenerateRequestId();
            var requestPayload = SerializeRequestMessage(payload, requestId, replyTo);

            // record the request state
            var requestState = new PendingRequestState(requestId, request, requestType, typeof(TResponseMessage), timeout);
            PendingRequests.TryAdd(requestId, requestState);

            try
            {
                Log.DebugFormat("Sending request message {0} to topic {1} with reply to {2} and payload size {3}", requestState, topic, replyTo, payload.Length);
                await Publish(requestType, topic, requestPayload);
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


        #endregion

        protected virtual byte[] SerializeRequestMessage(byte[] payload, string requestId, string replyTo)
        {
            // create the request wrapper message
            var requestMessage = new MessageWithHeaders(payload);
            requestMessage.Headers.Add(HeaderRequestId, requestId);
            requestMessage.Headers.Add(HeaderReplyTo, replyTo);

            var requestPayload = Settings.RequestResponse.MessageWithHeadersSerializer.Serialize(typeof(MessageWithHeaders), requestMessage);
            return requestPayload;
        }

        protected virtual byte[] SerializeResponseMessage(byte[] payload, string requestId)
        {
            // create the request wrapper message
            var responseMessage = new MessageWithHeaders(payload);
            responseMessage.Headers.Add(HeaderRequestId, requestId);

            var responsePayload = Settings.RequestResponse.MessageWithHeadersSerializer.Serialize(typeof(MessageWithHeaders), responseMessage);
            return responsePayload;
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
            var responseMessage = (MessageWithHeaders)Settings.RequestResponse.MessageWithHeadersSerializer.Deserialize(typeof(MessageWithHeaders), responsePayload);
            if (!responseMessage.Headers.TryGetValue(HeaderRequestId, out requestId))
            {
                Log.ErrorFormat("The response message arriving on topic {0} did not have the {1} header. Unable to math the response with the request. This likely indicates a misconfiguration.", topic, HeaderRequestId);
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
                // ToDo add a callback hook
                return;
            }

            try
            {
                if (Log.IsDebugEnabled)
                {
                    var tookTimespan = DateTime.Now.Subtract(requestState.Created);
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