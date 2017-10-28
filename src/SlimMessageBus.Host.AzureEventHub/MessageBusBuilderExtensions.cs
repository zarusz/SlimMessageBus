using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureEventHub
{
    public static class MessageBusBuilderExtensions
    {
        public static MessageBusBuilder WithProviderEventHub(this MessageBusBuilder mbb, EventHubMessageBusSettings eventHubSettings)
        {
            return mbb.WithProvider(settings => new EventHubMessageBus(settings, eventHubSettings));
        }
    }
}