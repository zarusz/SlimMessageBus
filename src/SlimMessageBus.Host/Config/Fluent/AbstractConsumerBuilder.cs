using System;

namespace SlimMessageBus.Host.Config
{
    public abstract class AbstractConsumerBuilder<T>
    {
        public Type MessageType { get; }

        public MessageBusSettings Settings { get; }

        protected AbstractConsumerBuilder(MessageBusSettings settings)
            : this(settings, typeof(T))
        {
        }

        protected AbstractConsumerBuilder(MessageBusSettings settings, Type messageType)
        {
            MessageType = messageType;
            Settings = settings;
        }

        public TBuilder AttachEvents<TBuilder>(Action<IConsumerEvents> eventsConfig)
            where TBuilder : AbstractConsumerBuilder<T>
        {
            if (eventsConfig == null) throw new ArgumentNullException(nameof(eventsConfig));

            eventsConfig(Settings);
            return (TBuilder)this;
        }
    }
}