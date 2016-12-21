using System;
using System.Collections.Generic;

namespace SlimMessageBus.Config
{
    public class MessageBusSettings
    {
        public IList<PublisherSettings> Publishers { get; }
        public IList<SubscriberSettings> Subscribers { get; }
        public IMessageSerializer Serializer { get; set; }
        public RequestResponseSettings RequestResponse { get; set; }

        public MessageBusSettings()
        {
            Publishers = new List<PublisherSettings>();
            Subscribers = new List<SubscriberSettings>();
            RequestResponse = new RequestResponseSettings();
        }
    }
}