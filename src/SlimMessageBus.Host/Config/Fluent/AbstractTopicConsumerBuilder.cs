namespace SlimMessageBus.Host.Config
{
    using System;

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
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));

            ConsumerSettings = new ConsumerSettings
            {
                Path = topic,
                MessageType = messageType
            };
            Settings.Consumers.Add(ConsumerSettings);
        }

        public TBuilder AttachEvents<TBuilder>(Action<IConsumerEvents> eventsConfig)
            where TBuilder : AbstractTopicConsumerBuilder
        {
            if (eventsConfig == null) throw new ArgumentNullException(nameof(eventsConfig));

            eventsConfig(ConsumerSettings);
            return (TBuilder)this;
        }
    }
}