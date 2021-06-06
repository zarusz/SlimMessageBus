namespace SlimMessageBus.Host.Config
{
    using System;

    public class ConsumerBuilder<T> : AbstractConsumerBuilder<T>
    {
        public ConsumerBuilder(MessageBusSettings settings)
            : base(settings)
        {
        }

        public ConsumerBuilder(MessageBusSettings settings, Type messageType)
            : base(settings, messageType)
        {
        }

        public TopicConsumerBuilder<T> Topic(string topic)
        {
            return new TopicConsumerBuilder<T>(topic, MessageType, Settings);
        }

        public TopicConsumerBuilder<T> Topic(string topic, Action<TopicConsumerBuilder<T>> topicConfig)
        {
            var b = Topic(topic);
            topicConfig(b);
            return b;
        }
    }
}