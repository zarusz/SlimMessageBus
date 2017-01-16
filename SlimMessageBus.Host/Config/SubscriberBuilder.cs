using System;

namespace SlimMessageBus.Host.Config
{
    public class SubscriberBuilder<T>
    {
        private readonly MessageBusSettings _settings;

        public SubscriberBuilder(MessageBusSettings settings)
        {
            _settings = settings;
        }

        public TopicSubscriberBuilder<T> Topic(string topic)
        {
            return new TopicSubscriberBuilder<T>(topic, _settings);
        }

        public TopicSubscriberBuilder<T> Topic(string topic, Action<TopicSubscriberBuilder<T>> topicConfig)
        {
            var b = Topic(topic);
            topicConfig(b);
            return b;
        }
    }
}