namespace SlimMessageBus.Host.Hybrid
{
    using System;
    using System.Collections.Generic;
    using SlimMessageBus.Host.Config;

    public class HybridMessageBusSettings : Dictionary<string, Action<MessageBusBuilder>>
    {        
    }
}