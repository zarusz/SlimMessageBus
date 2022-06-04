namespace SlimMessageBus.Host.Hybrid
{
    using SlimMessageBus.Host.Config;
    using System;

    public static class MessageBusBuilderExtensions
    {
        [Obsolete("Please use the new way of initializing the Hybrid message bus using the mbb.AddChildBus(...)")]
        public static MessageBusBuilder WithProviderHybrid(this MessageBusBuilder mbb, HybridMessageBusSettings hybridSettings) 
            => mbb.WithProvider(settings => new HybridMessageBus(settings, hybridSettings, mbb));

        public static MessageBusBuilder WithProviderHybrid(this MessageBusBuilder mbb) 
            => mbb.WithProvider(settings => new HybridMessageBus(settings, providerSettings: null, mbb));
    }
}