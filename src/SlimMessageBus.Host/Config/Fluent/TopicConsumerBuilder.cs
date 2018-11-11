using System;

namespace SlimMessageBus.Host.Config
{
    public abstract class TopicConsumerBuilder<TMessage>
    {
        public MessageBusSettings Settings { get; }
        public string Topic { get; }
        public Type MessageType { get; }
        public ConsumerSettings ConsumerSettings { get; }

        protected TopicConsumerBuilder(string topic, Type messageType, MessageBusSettings settings)
        {
            Topic = topic;
            MessageType = messageType;
            Settings = settings;

            ConsumerSettings = new ConsumerSettings
            {
                Topic = topic,
                MessageType = messageType
            };
            settings.Consumers.Add(ConsumerSettings);

        }
    }
}