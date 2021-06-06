namespace SlimMessageBus.Host.Hybrid
{
    using SlimMessageBus.Host.Config;

    public static class MessageBusBuilderExtensions
    {
        public static MessageBusBuilder WithProviderHybrid(this MessageBusBuilder mbb, HybridMessageBusSettings hybridSettings)
        {
            return mbb.WithProvider(settings => new HybridMessageBus(settings, hybridSettings));
        }
    }
}