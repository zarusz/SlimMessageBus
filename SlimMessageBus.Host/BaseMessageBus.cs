using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host
{
    public abstract class BaseMessageBus : IMessageBus
    {
        private static readonly ILog Log = LogManager.GetLogger<BaseMessageBus>();

        public MessageBusSettings Settings { get; }

        protected readonly IDictionary<Type, PublisherSettings> PublisherSettingsByMessageType;
        protected readonly ConcurrentDictionary<string, PendingRequestMessageState> RequestRegistry;

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        public CancellationToken CancellationToken => _cancellationTokenSource.Token;

        protected BaseMessageBus(MessageBusSettings settings)
        {
            Settings = settings;
            PublisherSettingsByMessageType = Settings.Publishers.ToDictionary(x => x.MessageType);
            RequestRegistry = new ConcurrentDictionary<string, PendingRequestMessageState>();
        }

        #region Implementation of IDisposable

        public virtual void Dispose()
        {
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

        public virtual async Task<TResponseMessage> Request<TResponseMessage>(IRequestMessage<TResponseMessage> request)
        {
            return await Request(request, Settings.RequestResponse.Timeout);
        }

        public virtual async Task<TResponseMessage> Request<TResponseMessage>(IRequestMessage<TResponseMessage> request, TimeSpan timeout)
        {
            var messageType = request.GetType();
            var topic = GetDefaultTopic(messageType);
            var replyTo = Settings.RequestResponse.Topic;

            // serialize the message
            var payload = Settings.Serializer.Serialize(messageType, request);

            // generate the request guid
            var requestId = GenerateRequestId();

            // create the request wrapper message
            var requestMessage = new MessageWithHeaders(payload);
            requestMessage.Headers.Add("request-id", requestId);
            requestMessage.Headers.Add("reply-to", replyTo);
            var requestPayload = Settings.RequestResponse.MessageWithHeadersSerializer.Serialize(typeof(MessageWithHeaders), requestMessage);

            // record the request state
            var requestResult = new PendingRequestMessageState(requestId, request, typeof(TResponseMessage), timeout);
            RequestRegistry.TryAdd(requestId, requestResult);

            try
            {
                Log.DebugFormat("Sending request message of type {0} to topic {1} with payload size {2} (reply-to: {3}, request-id: {4})", messageType, topic, payload.Length, replyTo, requestId);
                await Publish(messageType, topic, requestPayload);
            }
            catch (PublishMessageBusException e)
            {
                Log.DebugFormat("Publishing of request message failed: {0}", e);
                // remove from registry
                RequestRegistry.TryRemove(requestId, out requestResult);
                throw;
            }

            // convert Task<object> to Task<TResponseMessage>
            var typedTask = Convert<TResponseMessage>(requestResult.TaskCompletionSource.Task);
            return await typedTask;
        }

        #endregion

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