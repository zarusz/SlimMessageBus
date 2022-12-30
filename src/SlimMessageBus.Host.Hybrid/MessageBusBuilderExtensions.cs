namespace SlimMessageBus.Host.Hybrid;

using SlimMessageBus.Host.Config;

public static class MessageBusBuilderExtensions
{
    public static MessageBusBuilder WithProviderHybrid(this MessageBusBuilder mbb, HybridMessageBusSettings hybridSettings = null)
        => mbb.WithProvider(settings => new HybridMessageBus(settings, hybridSettings ?? new HybridMessageBusSettings(), mbb));
}