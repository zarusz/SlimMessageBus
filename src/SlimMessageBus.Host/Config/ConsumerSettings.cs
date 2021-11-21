namespace SlimMessageBus.Host.Config
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;

    public interface IMessageTypeConsumerInvokerSettings
    {
        /// <summary>
        /// Represents the type of the message that is expected on the topic.
        /// </summary>
        Type MessageType { get; }
        /// <summary>
        /// The consumer type that will handle the messages. An implementation of <see cref="IConsumer{TMessage}"/> or <see cref="IRequestHandler{TRequest,TResponse}"/>.
        /// </summary>
        Type ConsumerType { get; }
        /// <summary>
        /// The delegate to the consumer method responsible for accepting messages.
        /// </summary>
        Func<object, object, string, Task> ConsumerMethod { get; set; }
    }

    public class MessageTypeConsumerInvokerSettings : IMessageTypeConsumerInvokerSettings
    {
        /// <summary>
        /// Represents the type of the message that is expected on the topic.
        /// </summary>
        public Type MessageType { get; set; }
        /// <summary>
        /// The consumer type that will handle the messages. An implementation of <see cref="IConsumer{TMessage}"/> or <see cref="IRequestHandler{TRequest,TResponse}"/>.
        /// </summary>
        public Type ConsumerType { get; set; }
        /// <summary>
        /// The delegate to the consumer method responsible for accepting messages.
        /// </summary>
        public Func<object, object, string, Task> ConsumerMethod { get; set; }
    }

    public class ConsumerSettings : AbstractConsumerSettings, IMessageTypeConsumerInvokerSettings
    {
        private Type messageType;

        /// <inheritdoc/>
        public Type MessageType
        {
            get => messageType;
            set
            {
                messageType = value;
                CalculateResponseType();
            }
        }

        private void CalculateResponseType()
        {
            ResponseType = messageType
                .GetInterfaces()
                .Where(x => x.GetTypeInfo().IsGenericType && x.GetTypeInfo().GetGenericTypeDefinition() == typeof(IRequestMessage<>))
                .Select(x => x.GetGenericArguments()[0])
                .SingleOrDefault();
        }

        public ConsumerSettings()
        {
            ConsumersByMessageType = new Dictionary<Type, IMessageTypeConsumerInvokerSettings>();
        }

        /// Type of consumer that is configured (subscriber or request handler).
        /// </summary>
        public ConsumerMode ConsumerMode { get; set; }
        /// <inheritdoc/>
        public Type ConsumerType { get; set; }
        /// <inheritdoc/>
        public Func<object, object, string, Task> ConsumerMethod { get; set; }
        /// <summary>
        /// The delegate to the consumer method responsible for accepting messages.
        /// </summary>
        public Func<Task, object> ConsumerMethodResult { get; set; }
        /// <summary>
        /// A dictionary of all the known Consumers that handle any derived message type of the base type.
        /// </summary>
        public IDictionary<Type, IMessageTypeConsumerInvokerSettings> ConsumersByMessageType { get; }
        /// <summary>
        /// The response message that will be sent as a response to the arriving message (if request/response). Null when message type is not a request.
        /// </summary>
        public Type ResponseType { get; set; }
        /// <summary>
        /// Determines if the consumer setting is for request/response.
        /// </summary>
        public bool IsRequestMessage => ResponseType != null;
        /// <summary>
        /// Determines if a child scope is created for the message consuption. The consumer instance is then derived from that scope.
        /// </summary>
        public bool? IsMessageScopeEnabled { get; set; }
        /// <summary>
        /// Enables the disposal of consumer instance after the message has been consumed.
        /// </summary>
        public bool IsDisposeConsumerEnabled { get; set; }
    }
}