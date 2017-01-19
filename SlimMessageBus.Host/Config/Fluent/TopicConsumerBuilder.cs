namespace SlimMessageBus.Host.Config
{
    public abstract class TopicConsumerBuilder<T>
    {
        protected readonly MessageBusSettings Settings;
        public string Topic { get; }

        protected TopicConsumerBuilder(string topic, MessageBusSettings settings)
        {
            Topic = topic;
            Settings = settings;
        }
    }
}