using System;

namespace SlimMessageBus.Host.Config
{
    public class PublisherSettings : HasProviderExtensions
    {
        /// <summary>
        /// Message type that will be published.
        /// </summary>
        public Type MessageType { get; set; }
        /// <summary>
        /// Default topic name to use when not specified during publish/send operation.
        /// </summary>
        public string DefaultTopic { get; set; }
        /// <summary>
        /// Timeout after which this message should be considered as expired by the consumer.
        /// </summary>
        public TimeSpan? Timeout { get; set; }
    }
}