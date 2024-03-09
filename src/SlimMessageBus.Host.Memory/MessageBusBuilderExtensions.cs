﻿namespace SlimMessageBus.Host.Memory;

public static class MessageBusBuilderExtensions
{
    public static MemoryMessageBusBuilder WithProviderMemory(this MessageBusBuilder mbb, Action<MemoryMessageBusSettings> configure = null)
    {
#if NETSTANDARD2_0
        if (mbb is null) throw new ArgumentNullException(nameof(mbb));
#else
        ArgumentNullException.ThrowIfNull(mbb);
#endif

        var providerSettings = new MemoryMessageBusSettings();
        configure?.Invoke(providerSettings);

        return new MemoryMessageBusBuilder(mbb.WithProvider(settings => new MemoryMessageBus(settings, providerSettings)));
    }
}
