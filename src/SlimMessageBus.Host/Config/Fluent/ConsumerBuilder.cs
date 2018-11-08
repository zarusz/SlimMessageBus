using System;

namespace SlimMessageBus.Host.Config
{
    public abstract class ConsumerBuilder<T>
    {
        public Type MessageType => typeof(T);

        protected MessageBusSettings Settings { get; }

        protected ConsumerBuilder(MessageBusSettings settings)
        {
            Settings = settings;
        }
    }
}