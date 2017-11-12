namespace SlimMessageBus.Host.Config
{
    public abstract class TopicConsumerBuilder<T>
    {
        public MessageBusSettings Settings { get; }
        public string Topic { get; }

        protected TopicConsumerBuilder(string topic, MessageBusSettings settings)
        {
            Topic = topic;
            Settings = settings;
        }
    }
}