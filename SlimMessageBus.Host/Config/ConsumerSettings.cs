using System;
using System.Linq;

namespace SlimMessageBus.Host.Config
{
    public class ConsumerSettings : HasProviderExtensions
    {
        private Type _messageType;

        /// <summary>
        /// Represents the type of the message that is expected on the topic.
        /// </summary>
        public Type MessageType
        {
            get
            {
                return _messageType;
            }
            set
            {
                _messageType = value;

                ResponseType = _messageType
                    .GetInterfaces()
                    .Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof (IRequestMessage<>))
                    .Select(x => x.GetGenericArguments()[0])
                    .SingleOrDefault();
            }
        }

        /// <summary>
        /// The topic name.
        /// </summary>
        public string Topic { get; set; }
        /// <summary>
        /// The group the consumer will belong to.
        /// </summary>
        public string Group { get; set; }
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

        public ConsumerSettings()
        {
            Instances = 1;
        }
    }
}