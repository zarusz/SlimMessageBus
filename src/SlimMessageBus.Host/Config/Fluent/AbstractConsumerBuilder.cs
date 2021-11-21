namespace SlimMessageBus.Host.Config
{
    using System;

    public abstract class AbstractConsumerBuilder
    {
        public Type MessageType => ConsumerSettings.MessageType;

        public MessageBusSettings Settings { get; }

        public ConsumerSettings ConsumerSettings { get; }

        protected AbstractConsumerBuilder(MessageBusSettings settings, Type messageType, string path = null)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));

            ConsumerSettings = new ConsumerSettings
            {
                MessageType = messageType,
                Path = path,
            };
            Settings.Consumers.Add(ConsumerSettings);
        }

        public TBuilder AttachEvents<TBuilder>(Action<IConsumerEvents> eventsConfig)
            where TBuilder : AbstractConsumerBuilder
        {
            if (eventsConfig == null) throw new ArgumentNullException(nameof(eventsConfig));

            eventsConfig(ConsumerSettings);
            return (TBuilder)this;
        }

        public T Do<T>(Action<T> builder) where T : AbstractConsumerBuilder
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            builder((T)this);

            return (T)this;
        }
    }
}