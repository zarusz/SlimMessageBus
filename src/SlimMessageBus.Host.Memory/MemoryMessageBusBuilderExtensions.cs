namespace SlimMessageBus.Host.Memory
{
    using SlimMessageBus.Host.Config;
    using System;

    public static class MemoryMessageBusBuilderExtensions
    {
        public static MessageBusBuilder WithProviderMemory(this MessageBusBuilder mbb, MemoryMessageBusSettings providerSettings)
        {
            if (mbb is null) throw new ArgumentNullException(nameof(mbb));
            return mbb.WithProvider(settings => new MemoryMessageBus(settings, providerSettings));
        }
    }

}
