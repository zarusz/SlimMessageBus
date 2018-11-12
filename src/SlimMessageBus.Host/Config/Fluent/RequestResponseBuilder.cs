using System;

namespace SlimMessageBus.Host.Config
{
    public class RequestResponseBuilder
    {
        public RequestResponseSettings Settings;

        public RequestResponseBuilder(RequestResponseSettings settings)
        {
            Settings = settings;
        }

        public RequestResponseBuilder DefaultTimeout(TimeSpan timeout)
        {
            Settings.Timeout = timeout;
            return this;
        }

        public void ReplyToTopic(string topic)
        {
            Settings.Topic = topic;
        }
    }
}