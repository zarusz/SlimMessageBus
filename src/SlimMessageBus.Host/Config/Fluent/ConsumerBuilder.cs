using System;

namespace SlimMessageBus.Host.Config
{
    public abstract class ConsumerBuilder<T>
    {
        public Type MessageType { get; }

        public MessageBusSettings Settings { get; }

        protected ConsumerBuilder(MessageBusSettings settings)
            : this(settings, typeof(T))
        {
        }

        protected ConsumerBuilder(MessageBusSettings settings, Type messageType)
        {
            MessageType = messageType;
            Settings = settings;
        }
    }
}