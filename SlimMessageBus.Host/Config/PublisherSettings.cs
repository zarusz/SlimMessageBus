using System;

namespace SlimMessageBus.Config
{
    public class PublisherSettings
    {
        public Type MessageType { get; set; }
        public string DefaultTopic { get; set; }
    }
}