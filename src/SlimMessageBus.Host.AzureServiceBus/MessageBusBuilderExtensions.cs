namespace SlimMessageBus.Host.AzureServiceBus;

using SlimMessageBus.Host.Config;

public static class MessageBusBuilderExtensions
{
    public static MessageBusBuilder WithProviderServiceBus(this MessageBusBuilder mbb, ServiceBusMessageBusSettings providerSettings)
    {
        if (mbb is null) throw new ArgumentNullException(nameof(mbb));

        return mbb.WithProvider(settings => new ServiceBusMessageBus(settings, providerSettings));
    }
}