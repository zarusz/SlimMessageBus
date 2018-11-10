using System;

namespace SlimMessageBus.Host.Config
{
    public abstract class TopicConsumerBuilder<T>
    {
        public MessageBusSettings Settings { get; }
        public string Topic { get; }
        public Type MessageType { get; }

        protected TopicConsumerBuilder(string topic, Type messageType, MessageBusSettings settings)
        {
            Topic = topic;
            MessageType = messageType;
            Settings = settings;
        }
    }
}