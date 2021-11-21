namespace SlimMessageBus.Host.AzureEventHub
{
    using SlimMessageBus.Host.Config;
    using System;

    public static class MessageBusBuilderExtensions
    {
        public static MessageBusBuilder WithProviderEventHub(this MessageBusBuilder mbb, EventHubMessageBusSettings eventHubSettings)
        {
            if (mbb is null) throw new ArgumentNullException(nameof(mbb));

            return mbb.WithProvider(settings => new EventHubMessageBus(settings, eventHubSettings));
        }
    }
}