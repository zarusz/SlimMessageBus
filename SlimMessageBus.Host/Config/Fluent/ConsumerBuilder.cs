namespace SlimMessageBus.Host.Config
{
    public abstract class ConsumerBuilder<T>
    {
        protected readonly MessageBusSettings Settings;

        protected ConsumerBuilder(MessageBusSettings settings)
        {
            Settings = settings;
        }
    }
}