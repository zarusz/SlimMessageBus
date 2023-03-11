namespace SlimMessageBus.Host.AzureEventHub;

public static class MessageBusBuilderExtensions
{
    public static MessageBusBuilder WithProviderEventHub(this MessageBusBuilder mbb, EventHubMessageBusSettings eventHubSettings)
    {
        if (mbb is null) throw new ArgumentNullException(nameof(mbb));

        return mbb.WithProvider(settings => new EventHubMessageBus(settings, eventHubSettings));
    }
}