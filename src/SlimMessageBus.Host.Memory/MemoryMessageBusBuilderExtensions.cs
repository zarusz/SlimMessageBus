using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.Memory
{
    public static class MemoryMessageBusBuilderExtensions
    {
        public static MessageBusBuilder WithProviderMemory(this MessageBusBuilder mbb, MemoryMessageBusSettings providerSettings)
        {
            return mbb.WithProvider(settings => new MemoryMessageBus(settings, providerSettings));
        }
    }

}
