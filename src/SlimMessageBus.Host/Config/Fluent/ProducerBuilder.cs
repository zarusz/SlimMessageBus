using System;

namespace SlimMessageBus.Host.Config
{
    public class ProducerBuilder<T>
    {
        public ProducerSettings Settings { get; }

        public ProducerBuilder(ProducerSettings settings)
            : this(settings, typeof(T))
        {
        }

        public ProducerBuilder(ProducerSettings settings, Type messageType)
        {
            Settings = settings;
            Settings.MessageType = messageType;
        }

        public ProducerBuilder<T> DefaultTopic(string name)
        {
            Settings.DefaultTopic = name;
            return this;
        }

        public ProducerBuilder<T> DefaultTimeout(TimeSpan timeout)
        {
            Settings.Timeout = timeout;
            return this;
        }
    }
}