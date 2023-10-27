namespace SlimMessageBus.Host.AzureServiceBus;

internal class ServiceBusMessageBusSettingsValidationService : DefaultMessageBusSettingsValidationService<ServiceBusMessageBusSettings>
{
    public ServiceBusMessageBusSettingsValidationService(MessageBusSettings settings, ServiceBusMessageBusSettings providerSettings) : base(settings, providerSettings)
    {
    }

    public override void AssertSettings()
    {
        base.AssertSettings();

        if (string.IsNullOrEmpty(ProviderSettings.ConnectionString))
        {
            ThrowFieldNotSet(nameof(ProviderSettings.ConnectionString));
        }

        var kindMapping = new KindMapping();
        // This will validae if one path is mapped to both a topic and a queue
        kindMapping.Configure(Settings);
    }

    protected override void AssertConsumer(ConsumerSettings consumerSettings)
    {
        base.AssertConsumer(consumerSettings);

        if (consumerSettings.PathKind == PathKind.Topic && string.IsNullOrEmpty(consumerSettings.GetSubscriptionName(required: false)))
        {
            ThrowConsumerFieldNotSet(consumerSettings, nameof(AsbConsumerBuilderExtensions.SubscriptionName));
        }
    }
}
