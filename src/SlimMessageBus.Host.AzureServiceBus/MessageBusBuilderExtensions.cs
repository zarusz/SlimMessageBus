using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureServiceBus
{
    public static class MessageBusBuilderExtensions
    {
        public static MessageBusBuilder WithProviderServiceBus(this MessageBusBuilder mbb, ServiceBusMessageBusSettings serviceBusSettings)
        {
            return mbb.WithProvider(settings => new ServiceBusMessageBus(settings, serviceBusSettings));
        }
    }
}