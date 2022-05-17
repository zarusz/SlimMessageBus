namespace SlimMessageBus.Host.Config
{
    using System;

    public class DiscoveryConsumerType
    {
        public Type ConsumerType { get; set; }
        public Type MessageType { get; set; }
        public Type ResponseType { get; set; }
    }
}