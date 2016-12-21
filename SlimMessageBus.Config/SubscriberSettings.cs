using System;

namespace SlimMessageBus.Config
{
    public class SubscriberSettings
    {
        public Type MessageType { get; set; }
        public string Topic { get; set; }
    }
}