using System;
using System.Linq;
using System.Reflection;

namespace SlimMessageBus.Host.Config
{
    public class ConsumerSettings : HasProviderExtensions, IConsumerEvents
    {
        private Type _messageType;

        /// <summary>
        /// Represents the type of the message that is expected on the topic.
        /// </summary>
        public Type MessageType
        {
            get => _messageType;
            set
            {
                _messageType = value;
                CalculateResponseType();
            }
        }

        private void CalculateResponseType()
        {
            ResponseType = _messageType
                .GetInterfaces()
                .Where(x => x.GetTypeInfo().IsGenericType && x.GetTypeInfo().GetGenericTypeDefinition() == typeof(IRequestMessage<>))
                .Select(x => x.GetGenericArguments()[0])
                .SingleOrDefault();
        }

        /// <summary>
        /// The topic name.
        /// </summary>
        public string Topic { get; set; }
        /// <summary>
        /// Number of consumer instances created for this app domain.
        /// </summary>
        public int Instances { get; set; }
        /// <summary>
        /// The consumer type that will handle the messages. An implementation of <see cref="IConsumer{TMessage}"/> or <see cref="IRequestHandler{TRequest,TResponse}"/>.
        /// </summary>
        public Type ConsumerType { get; set; }
        /// <summary>
        /// Type of consumer that is configured (subscriber or request handler).
        /// </summary>
        public ConsumerMode ConsumerMode { get; set; }
        /// <summary>
        /// The response message that will be sent as a response to the arriving message (if request/response). Null when message type is not a request.
        /// </summary>
        public Type ResponseType { get; set; }
        /// <summary>
        /// Determines if the consumer setting is for request/response.
        /// </summary>
        public bool IsRequestMessage => ResponseType != null;

        #region Implementation of IConsumerEvents

        /// <summary>
        /// Called whenever a consumer receives an expired message.
        /// </summary>
        public Action<ConsumerSettings, object> OnMessageExpired { get; set; }
        /// <summary>
        /// Called whenever a consumer errors out while processing the message.
        /// </summary>
        public Action<ConsumerSettings, object, Exception> OnMessageFault { get; set; }

        #endregion

        public ConsumerSettings()
        {
            Instances = 1;
        }
    }
}