using System.Collections.Generic;

namespace SlimMessageBus.Host.Config
{
    public class MessageBusSettings
    {
        public IList<PublisherSettings> Publishers { get; }
        public IList<SubscriberSettings> Subscribers { get; }
        public RequestResponseSettings RequestResponse { get; set; }
        public IMessageSerializer Serializer { get; set; }
        /// <summary>
        /// Dedicated <see cref="IMessageSerializer"/> capable of serializing <see cref="MessageWithHeaders"/>.
        /// By default uses <see cref="MessageWithHeadersSerializer"/>.
        /// </summary>
        public IMessageSerializer MessageWithHeadersSerializer { get; set; }
        public IDependencyResolver DependencyResolver { get; set; }

        public MessageBusSettings()
        {
            Publishers = new List<PublisherSettings>();
            Subscribers = new List<SubscriberSettings>();
            MessageWithHeadersSerializer = new MessageWithHeadersSerializer();
        }
    }
}