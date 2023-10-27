namespace SlimMessageBus.Host.Kafka;

using SlimMessageBus.Host.Services;

internal class KafkaMessageBusSettingsValidationService : DefaultMessageBusSettingsValidationService<KafkaMessageBusSettings>
{
    public KafkaMessageBusSettingsValidationService(MessageBusSettings settings, KafkaMessageBusSettings providerSettings) : base(settings, providerSettings)
    {
    }

    public override void AssertSettings()
    {
        base.AssertSettings();

        if (string.IsNullOrEmpty(ProviderSettings.BrokerList))
        {
            ThrowFieldNotSet(nameof(KafkaMessageBusSettings.BrokerList));
        }
    }

    protected override void AssertConsumer(ConsumerSettings consumerSettings)
    {
        base.AssertConsumer(consumerSettings);

        if (consumerSettings.GetGroup() == null)
        {
            ThrowConsumerFieldNotSet(consumerSettings, nameof(BuilderExtensions.KafkaGroup));
        }
    }

    protected override void AssertRequestResponseSettings()
    {
        base.AssertRequestResponseSettings();

        if (Settings.RequestResponse != null)
        {
            if (Settings.RequestResponse.GetGroup() == null)
            {
                ThrowRequestResponseFieldNotSet(nameof(BuilderExtensions.KafkaGroup));
            }

            if (Settings.Consumers.Any(x => x.GetGroup() == Settings.RequestResponse.GetGroup() && x.Path == Settings.RequestResponse.Path))
            {
                ThrowRequestResponseFieldNotSet(nameof(BuilderExtensions.KafkaGroup), "cannot use topic that is already being used by a consumer");
            }
        }
    }
}
