using System;
using System.Collections.Generic;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.Hybrid
{
    public class HybridMessageBusSettings : Dictionary<string, Action<MessageBusBuilder>>
    {        
    }
}