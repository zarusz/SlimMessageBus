namespace SlimMessageBus.Host.Config
{
    using System;
    using System.Collections.Generic;

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

        /// <summary>
        /// Hook called whenver message is being produced. Can be used to add (or mutate) message headers.
        /// </summary>
        public ProducerBuilder<T> WithHeaderModifier(Action<IDictionary<string, object>, T> headerModifierAction)
        {
            if (headerModifierAction == null) throw new ArgumentNullException(nameof(headerModifierAction));

            Settings.HeaderModifier = (headers, message) => headerModifierAction(headers, (T)message);
            return this;
        }
    }
}