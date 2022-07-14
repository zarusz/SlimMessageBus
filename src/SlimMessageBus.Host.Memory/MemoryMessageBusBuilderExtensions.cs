namespace SlimMessageBus.Host.Memory
{
    using SlimMessageBus.Host.Config;
    using System;

    public static class MemoryMessageBusBuilderExtensions
    {
        public static MemoryMessageBusBuilder WithProviderMemory(this MessageBusBuilder mbb, MemoryMessageBusSettings providerSettings)
        {
            if (mbb is null) throw new ArgumentNullException(nameof(mbb));
            if (providerSettings is null) throw new ArgumentNullException(nameof(providerSettings));

            return new MemoryMessageBusBuilder(mbb.WithProvider(settings => new MemoryMessageBus(settings, providerSettings)));
        }
    }
}
