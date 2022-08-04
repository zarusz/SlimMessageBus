namespace SlimMessageBus.Host.Memory;

using SlimMessageBus.Host.Config;

public static class MemoryMessageBusBuilderExtensions
{
    public static MemoryMessageBusBuilder WithProviderMemory(this MessageBusBuilder mbb, MemoryMessageBusSettings providerSettings = null)
    {
        if (mbb is null) throw new ArgumentNullException(nameof(mbb));
        
        providerSettings ??= new MemoryMessageBusSettings();

        return new MemoryMessageBusBuilder(mbb.WithProvider(settings => new MemoryMessageBus(settings, providerSettings)));
    }
}
