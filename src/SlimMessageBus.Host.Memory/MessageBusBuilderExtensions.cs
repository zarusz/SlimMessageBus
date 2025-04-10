namespace SlimMessageBus.Host.Memory;

public static class MessageBusBuilderExtensions
{
    public static MemoryMessageBusBuilder WithProviderMemory(this MessageBusBuilder mbb, Action<MemoryMessageBusSettings> configure = null)
    {
        if (mbb is null) throw new ArgumentNullException(nameof(mbb));

        var providerSettings = new MemoryMessageBusSettings();
        configure?.Invoke(providerSettings);

        return new MemoryMessageBusBuilder(mbb.WithProvider(settings => new MemoryMessageBus(settings, providerSettings)));
    }
}
