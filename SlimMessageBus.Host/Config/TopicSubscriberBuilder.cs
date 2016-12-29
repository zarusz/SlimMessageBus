using System;
using System.Linq;

namespace SlimMessageBus.Host.Config
{
    public class SubscriberBuilder<T>
    {
        private readonly MessageBusSettings _settings;

        public SubscriberBuilder(MessageBusSettings settings)
        {
            _settings = settings;
        }

        public TopicSubscriberBuilder<T> OnTopic(string topic)
        {
            return new TopicSubscriberBuilder<T>(topic, _settings);
        }

        public TopicSubscriberBuilder<T> OnTopic(string topic, Action<TopicSubscriberBuilder<T>> topicConfig)
        {
            var b = OnTopic(topic);
            topicConfig(b);
            return b;
        }
    }


    public class TopicSubscriberBuilder<T>
    {
        private readonly MessageBusSettings _settings;
        public string Topic { get; }

        public TopicSubscriberBuilder(string topic, MessageBusSettings settings)
        {
            Topic = topic;
            _settings = settings;
        }

        public GroupSubscriberBuilder<T> Group(string group)
        {
            return new GroupSubscriberBuilder<T>(group, _settings, this);
        }
    }

    public class GroupSubscriberBuilder<T>
    {
        private readonly SubscriberSettings _subscriberSettings;
        public string Group { get; }

        public GroupSubscriberBuilder(string group, MessageBusSettings settings, TopicSubscriberBuilder<T> topicSubscriberBuilder)
        {
            Group = group;

            var subscriberSettingsExist = settings.Subscribers.Any(x => x.Group == group && x.Topic == topicSubscriberBuilder.Topic);
            Assert.IsFalse(subscriberSettingsExist, 
                () => new ConfigurationMessageBusException($"Group '{group}' configuration for topic '{topicSubscriberBuilder.Topic}' already exists"));

            _subscriberSettings = new SubscriberSettings()
            {
                MessageType = typeof(T),
                Topic = topicSubscriberBuilder.Topic,
                Group = group
            };
            settings.Subscribers.Add(_subscriberSettings);
        }

        public GroupSubscriberBuilder<T> WithConsumer<TConsumer>()
            where TConsumer : ISubscriber<T>
        {
            _subscriberSettings.ConsumerType = typeof(TConsumer);
            return this;
        }

        public GroupSubscriberBuilder<T> WithRequestHandler<TConsumer, TResponse, TRequest>()
            where TRequest : IRequestMessage<TResponse>
            where TConsumer : IRequestHandler<TRequest, TResponse>
        {
            _subscriberSettings.ConsumerType = typeof(TConsumer);
            return this;
        }

        public GroupSubscriberBuilder<T> Instances(int numberOfInstances)
        {
            _subscriberSettings.Instances = numberOfInstances;
            return this;
        }
    }
}