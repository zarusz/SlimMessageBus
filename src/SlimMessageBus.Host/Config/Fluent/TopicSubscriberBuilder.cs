namespace SlimMessageBus.Host.Config
{
    public class TopicSubscriberBuilder<T> : TopicConsumerBuilder<T>
    {
        public TopicSubscriberBuilder(string topic, MessageBusSettings settings)
            : base(topic, settings)
        {
        }

        public GroupSubscriberBuilder<T> Group(string group)
        {
            return new GroupSubscriberBuilder<T>(group, Topic, Settings);
        }
    }
}