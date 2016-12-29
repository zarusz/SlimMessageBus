using System;
using System.Collections.Generic;

namespace SlimMessageBus.Host.Config
{
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
        /// The consumer type that will handle the messages. An implementation of <see cref="ISubscriber{TMessage}"/>.
        /// </summary>
        public Type ConsumerType { get; set; }

        public SubscriberSettings()
        {
            Instances = 1;
        }
    }
}