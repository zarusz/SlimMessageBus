using System;
using System.Collections.Generic;

namespace SlimMessageBus.Host.Config
{
    public class MessageBusSettings : IConsumerEvents
    {
        public IList<PublisherSettings> Publishers { get; }
        public IList<ConsumerSettings> Consumers { get; }
        public RequestResponseSettings RequestResponse { get; set; }
        public IMessageSerializer Serializer { get; set; }
        /// <summary>
        /// Dedicated <see cref="IMessageSerializer"/> capable of serializing <see cref="MessageWithHeaders"/>.
        /// By default uses <see cref="MessageWithHeadersSerializer"/>.
        /// </summary>
        public IMessageSerializer MessageWithHeadersSerializer { get; set; }
        public IDependencyResolver DependencyResolver { get; set; }

        #region Implementation of IConsumerEvents

        public Action<ConsumerSettings, object> OnMessageExpired { get; set; }
        public Action<ConsumerSettings, object, Exception> OnMessageFault { get; set; }

        #endregion

        public MessageBusSettings()
        {
            Publishers = new List<PublisherSettings>();
            Consumers = new List<ConsumerSettings>();
            MessageWithHeadersSerializer = new MessageWithHeadersSerializer();
        }
    }
}