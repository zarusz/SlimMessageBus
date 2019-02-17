using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.Hybrid
{
    public static class MessageBusBuilderExtensions
    {
        public static MessageBusBuilder WithProviderHybrid(this MessageBusBuilder mbb, HybridMessageBusSettings hybridSettings)
        {
            return mbb.WithProvider(settings => new HybridMessageBus(settings, hybridSettings));
        }
    }
}