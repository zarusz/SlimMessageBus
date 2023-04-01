namespace SlimMessageBus.Host.AzureServiceBus;

public static class MessageBusBuilderExtensions
{
    public static MessageBusBuilder WithProviderServiceBus(this MessageBusBuilder mbb, Action<ServiceBusMessageBusSettings> configure)
    {
        if (mbb is null) throw new ArgumentNullException(nameof(mbb));
        if (configure == null) throw new ArgumentNullException(nameof(configure));

        var providerSettings = new ServiceBusMessageBusSettings();
        configure(providerSettings);

        return mbb.WithProvider(settings => new ServiceBusMessageBus(settings, providerSettings));
    }
}