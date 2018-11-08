using System;

namespace SlimMessageBus.Host.Config
{
    public class PublisherBuilder<T>
    {
        public Type MessageType => typeof(T);

        public PublisherSettings Settings { get; }

        public PublisherBuilder(PublisherSettings settings)
        {
            Settings = settings;
            Settings.MessageType = typeof (T);
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