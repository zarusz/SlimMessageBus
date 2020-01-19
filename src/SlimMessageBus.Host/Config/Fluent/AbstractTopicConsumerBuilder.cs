using System;

namespace SlimMessageBus.Host.Config
{
    public abstract class AbstractTopicConsumerBuilder
    {
        public MessageBusSettings Settings { get; }
        public Type MessageType { get; }
        public string Topic { get; }
        public ConsumerSettings ConsumerSettings { get; }

        protected AbstractTopicConsumerBuilder(string topic, Type messageType, MessageBusSettings settings)
        {
            Topic = topic;
            MessageType = messageType;
            Settings = settings;

            ConsumerSettings = new ConsumerSettings
            {
                Topic = topic,
                MessageType = messageType
            };
            Settings.Consumers.Add(ConsumerSettings);
        }
    }
}