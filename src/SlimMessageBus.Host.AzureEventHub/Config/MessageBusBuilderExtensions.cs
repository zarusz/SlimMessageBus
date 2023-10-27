namespace SlimMessageBus.Host.AzureEventHub;

using SlimMessageBus.Host;

public static class MessageBusBuilderExtensions
{
    public static MessageBusBuilder WithProviderEventHub(this MessageBusBuilder mbb, Action<EventHubMessageBusSettings> configure)
    {
        if (mbb is null) throw new ArgumentNullException(nameof(mbb));
        if (configure == null) throw new ArgumentNullException(nameof(configure));

        var providerSettings = new EventHubMessageBusSettings();
        configure(providerSettings);

        return mbb.WithProvider(settings => new EventHubMessageBus(settings, providerSettings));
    }
}