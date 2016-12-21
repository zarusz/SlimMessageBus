using System;

namespace SlimMessageBus.Config
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

        public void OnTopic(string topic)
        {
            _settings.Topic = topic;
        }
    }
}