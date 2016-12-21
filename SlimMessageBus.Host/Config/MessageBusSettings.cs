using System;
using System.Collections.Generic;
using SlimMessageBus.Host;

namespace SlimMessageBus.Config
{
    public class MessageBusSettings
    {
        public IList<PublisherSettings> Publishers { get; }
        public IList<SubscriberSettings> Subscribers { get; }
        public RequestResponseSettings RequestResponse { get; set; }
        public IMessageSerializer Serializer { get; set; }
        public ISubscriberResolver SubscriberResolver { get; set; }

        public MessageBusSettings()
        {
            Publishers = new List<PublisherSettings>();
            Subscribers = new List<SubscriberSettings>();
            RequestResponse = new RequestResponseSettings();
        }
    }
}