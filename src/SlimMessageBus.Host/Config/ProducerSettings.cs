namespace SlimMessageBus.Host.Config
{
    using System;

    public class ProducerSettings : HasProviderExtensions, IProducerEvents
    {
        /// <summary>
        /// Message type that will be published.
        /// </summary>
        public Type MessageType { get; set; }
        /// <summary>
        /// Default topic/queue name to use when not specified during publish/send operation.
        /// </summary>
        public string DefaultPath { get; set; }
        /// <summary>
        /// Determines the kind of the path 
        /// </summary>
        public PathKind PathKind { get; set; } = PathKind.Topic;
        /// <summary>
        /// Timeout after which this message should be considered as expired by the consumer.
        /// </summary>
        public TimeSpan? Timeout { get; set; }
        
        #region IProducerEvents

        /// <inheritdoc/>
        public Action<IMessageBus, ProducerSettings, object, string> OnMessageProduced { get; set; }

        #endregion
    }

}