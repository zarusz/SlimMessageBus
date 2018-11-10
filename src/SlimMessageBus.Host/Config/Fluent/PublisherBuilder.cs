using System;

namespace SlimMessageBus.Host.Config
{
    public class PublisherBuilder<T>
    {
        public Type MessageType { get; }

        public PublisherSettings Settings { get; }

        public PublisherBuilder(PublisherSettings settings)
            : this(settings, typeof(T))
        {
        }

        public PublisherBuilder(PublisherSettings settings, Type messageType)
        {
            MessageType = messageType;
            Settings = settings;
            Settings.MessageType = messageType;
        }

        public PublisherBuilder<T> DefaultTopic(string name)
        {
            Settings.DefaultTopic = name;
            return this;
        }

        public PublisherBuilder<T> DefaultTimeout(TimeSpan timeout)
        {
            Settings.Timeout = timeout;
            return this;
        }
    }
}