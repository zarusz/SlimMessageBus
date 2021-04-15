using System;

namespace SlimMessageBus.Host.Config
{
    public abstract class AbstractConsumerSettings : HasProviderExtensions, IConsumerEvents
    {
        /// <summary>
        /// The topic or queue name.
        /// </summary>
        public string Topic { get; set; }
        /// <summary>
        /// Number of concurrent competing consumer instances to be created for the bus.
        /// This dictates how many concurrent messages can be processed at a time.
        /// </summary>
        public int Instances { get; set; }

        #region Implementation of IConsumerEvents

        ///
        /// <inheritdoc/>
        ///
        public Action<IMessageBus, AbstractConsumerSettings, object, string, object> OnMessageArrived { get; set; }

        ///
        /// <inheritdoc/>
        ///
        public Action<IMessageBus, AbstractConsumerSettings, object, string, object> OnMessageFinished { get; set; }

        ///
        /// <inheritdoc/>
        ///
        public Action<IMessageBus, AbstractConsumerSettings, object, object> OnMessageExpired { get; set; }

        ///
        /// <inheritdoc/>
        ///
        public Action<IMessageBus, AbstractConsumerSettings, object, Exception, object> OnMessageFault { get; set; }

        #endregion

        protected AbstractConsumerSettings()
        {
            Instances = 1;
        }
    }
}