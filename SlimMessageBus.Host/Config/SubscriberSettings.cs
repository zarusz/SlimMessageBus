using System;

namespace SlimMessageBus.Host.Config
{
    public enum ConsumerMode
    {
        Subscriber,
        RequestResponse,
    }

    public class SubscriberSettings : HasProviderExtensions
    {
        /// <summary>
        /// Represents the type of the message that is expected on the topic.
        /// </summary>
        public Type MessageType { get; set; }
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
        /// The consumer type that will handle the messages. An implementation of <see cref="ISubscriber{TMessage}"/> or <see cref="IRequestHandler{TRequest,TResponse}"/>.
        /// </summary>
        public Type ConsumerType { get; set; }
        public ConsumerMode ConsumerMode { get; set; }
        public Type ResponseType { get; set; }

        public bool IsRequestMessage => ResponseType != null;

        public SubscriberSettings()
        {
            Instances = 1;
        }
    }
}