namespace SlimMessageBus.Host.AzureEventHub
{
    using SlimMessageBus.Host.Config;

    public static class MessageBusBuilderExtensions
    {
        public static MessageBusBuilder WithProviderEventHub(this MessageBusBuilder mbb, EventHubMessageBusSettings eventHubSettings)
        {
            return mbb.WithProvider(settings => new EventHubMessageBus(settings, eventHubSettings));
        }
    }
}