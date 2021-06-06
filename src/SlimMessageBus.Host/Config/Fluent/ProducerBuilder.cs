namespace SlimMessageBus.Host.Config
{
    using System;

    public class ProducerBuilder<T>
    {
        public ProducerSettings Settings { get; }

        public ProducerBuilder(ProducerSettings settings)
            : this(settings, typeof(T))
        {
        }

        public ProducerBuilder(ProducerSettings settings, Type messageType)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Settings.MessageType = messageType;
        }

        public ProducerBuilder<T> DefaultTopic(string name)
        {
            Settings.DefaultPath = name ?? throw new ArgumentNullException(nameof(name));
            return this;
        }

        public ProducerBuilder<T> DefaultTimeout(TimeSpan timeout)
        {
            Settings.Timeout = timeout;
            return this;
        }

        public ProducerBuilder<T> AttachEvents(Action<IProducerEvents> eventsConfig)
        {
            if (eventsConfig == null) throw new ArgumentNullException(nameof(eventsConfig));

            eventsConfig(Settings);
            return this;
        }
    }
}