using System;
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

        public MessageBusSettings Settings { get; protected set; }
        protected readonly IDictionary<Type, PublisherSettings> PublisherSettingsByType;

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        public CancellationToken CancellationToken => _cancellationTokenSource.Token;

        protected BaseMessageBus(MessageBusSettings settings)
        {
            Settings = settings;
            PublisherSettingsByType = Settings.Publishers.ToDictionary(x => x.MessageType);
        }

        #region Implementation of IDisposable

        public virtual void Dispose()
        {
        }

        #endregion

        protected abstract Task Publish(Type type, string topic, byte[] payload, string replyTo = null);

        #region Implementation of IPublishBus

        public virtual async Task Publish<TMessage>(TMessage message, string topic = null)
        {
            var messageType = typeof (TMessage);

            var payload = Settings.Serializer.Serialize(messageType, message);

            if (topic == null)
            {
                // when topic was not provided, lookup default topic from configuration

                PublisherSettings publisherSettings;
                if (!PublisherSettingsByType.TryGetValue(messageType, out publisherSettings))
                {
                    throw new PublishMessageBusException($"Message of type {messageType} was not registered as a supported publish message. Please check your MessageBus configuration and include this type.");
                }
                topic = publisherSettings.DefaultTopic;
                Log.DebugFormat("Applying default topic {0} for message type {1}", topic, messageType);
            }

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
            throw new NotImplementedException();
        }

        #endregion
    }
}