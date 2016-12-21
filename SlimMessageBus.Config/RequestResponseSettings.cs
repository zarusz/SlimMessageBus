using System;

namespace SlimMessageBus.Config
{
    public class RequestResponseSettings
    {
        public TimeSpan Timeout { get; set; }
        public string Topic { get; set; }
    }
}