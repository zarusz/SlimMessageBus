namespace SlimMessageBus.Host.AzureServiceBus;

internal class ServiceBusMessageBusSettingsValidationService(MessageBusSettings settings, ServiceBusMessageBusSettings providerSettings)
    : DefaultMessageBusSettingsValidationService<ServiceBusMessageBusSettings>(settings, providerSettings)
{
    public override void AssertSettings()
    {
        if (string.IsNullOrEmpty(ProviderSettings.ConnectionString))
        {
            ThrowFieldNotSet(nameof(ProviderSettings.ConnectionString));
        }

        var kindMapping = new KindMapping();
        // This will validate if one path is mapped to both a topic and a queue
        kindMapping.Configure(Settings);

        // Base as the last to give more specific errors first
        base.AssertSettings();
    }

    protected override void AssertProducer(ProducerSettings producerSettings)
    {
        // Note: Not calling base, as we allow to not set DefaultPath for producers as the path is determined during Publish
    }

    protected override void AssertConsumer(ConsumerSettings consumerSettings)
    {
        if (string.IsNullOrEmpty(consumerSettings.Path))
        {
            ThrowConsumerFieldNotSet(consumerSettings,
                [
                    nameof(AsbConsumerBuilderExtensions.Queue),
                    nameof(ConsumerBuilder<object>.Topic)
                ]);
        }

        if (consumerSettings.PathKind == PathKind.Topic && string.IsNullOrEmpty(consumerSettings.GetSubscriptionName(ProviderSettings)))
        {
            ThrowConsumerFieldNotSet(consumerSettings, nameof(AsbConsumerBuilderExtensions.SubscriptionName));
        }

        // Base as the last to give more specific errors first
        base.AssertConsumer(consumerSettings);
    }
}
