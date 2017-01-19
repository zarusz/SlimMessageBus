using System;

namespace SlimMessageBus.Host.Config
{
    public class RequestResponseBuilder
    {
        private readonly RequestResponseSettings _settings;

        public RequestResponseBuilder(RequestResponseSettings settings)
        {
            _settings = settings;
        }

        public RequestResponseBuilder DefaultTimeout(TimeSpan timeout)
        {
            _settings.Timeout = timeout;
            return this;
        }

        public void ReplyToTopic(string topic)
        {
            _settings.Topic = topic;
        }

        public void Group(string group)
        {
            _settings.Group = group;
        }
    }
}