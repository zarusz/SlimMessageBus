using System;

namespace SlimMessageBus.Host.Config
{
    public class RequestResponseSettings
    {
        public TimeSpan Timeout { get; set; }
        public string Topic { get; set; }
        public IMessageSerializer MessageWithHeadersSerializer { get; set; }

        public RequestResponseSettings()
        {
            MessageWithHeadersSerializer = new MessageWithHeadersSerializer();
        }
    }
}