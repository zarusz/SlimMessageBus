namespace SlimMessageBus.Host.Memory;

using SlimMessageBus.Host;

public static class MessageBusBuilderExtensions
{
    public static MemoryMessageBusBuilder WithProviderMemory(this MessageBusBuilder mbb, MemoryMessageBusSettings providerSettings = null)
    {
        if (mbb is null) throw new ArgumentNullException(nameof(mbb));

        providerSettings ??= new MemoryMessageBusSettings();

        return new MemoryMessageBusBuilder(mbb.WithProvider(settings => new MemoryMessageBus(settings, providerSettings)));
    }
}
