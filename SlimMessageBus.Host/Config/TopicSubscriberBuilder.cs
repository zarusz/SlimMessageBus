namespace SlimMessageBus.Host.Config
{
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
}