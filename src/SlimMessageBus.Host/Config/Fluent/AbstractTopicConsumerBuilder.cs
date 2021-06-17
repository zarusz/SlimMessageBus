namespace SlimMessageBus.Host.Config
{
    using System;

    public abstract class AbstractTopicConsumerBuilder
    {
        public MessageBusSettings Settings { get; }
        public Type MessageType { get; }
        public string Path { get; }
        public ConsumerSettings ConsumerSettings { get; }

        protected AbstractTopicConsumerBuilder(string path, Type messageType, MessageBusSettings settings)
        {
            Path = path;
            MessageType = messageType;
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));

            ConsumerSettings = new ConsumerSettings
            {
                Path = path,
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