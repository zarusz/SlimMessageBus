namespace SlimMessageBus.Host.Config
{
    public abstract class ConsumerBuilder<T>
    {
        protected MessageBusSettings Settings { get; }

        protected ConsumerBuilder(MessageBusSettings settings)
        {
            Settings = settings;
        }
    }
}