namespace SlimMessageBus.Host.GooglePubSub;

internal class GooglePubSubMessageBusSettingsValidationService(
    MessageBusSettings settings,
    GooglePubSubMessageBusSettings providerSettings)
    : DefaultMessageBusSettingsValidationService<GooglePubSubMessageBusSettings>(settings, providerSettings)
{
    public override void AssertSettings()
    {
        if (string.IsNullOrWhiteSpace(ProviderSettings.ProjectId))
        {
            ThrowFieldNotSet(nameof(ProviderSettings.ProjectId));
        }
        if (ProviderSettings.PublisherClientFactory == null) ThrowFieldNotSet(nameof(ProviderSettings.PublisherClientFactory));
        if (ProviderSettings.SubscriberClientFactory == null) ThrowFieldNotSet(nameof(ProviderSettings.SubscriberClientFactory));
        if (ProviderSettings.HeaderSerializer == null) ThrowFieldNotSet(nameof(ProviderSettings.HeaderSerializer));

        base.AssertSettings();
    }

    protected override void AssertProducer(ProducerSettings producerSettings)
    {
        if (producerSettings.PathKind != PathKind.Topic)
        {
            ThrowProducerFieldNotSet(producerSettings, nameof(producerSettings.PathKind), "must be Topic for Google Cloud Pub/Sub");
        }
        base.AssertProducer(producerSettings);
    }

    protected override void AssertConsumer(ConsumerSettings consumerSettings)
    {
        if (consumerSettings.PathKind != PathKind.Topic)
        {
            ThrowConsumerFieldNotSet(consumerSettings, nameof(consumerSettings.PathKind));
        }
        if (string.IsNullOrWhiteSpace(consumerSettings.GetSubscriptionName()))
        {
            ThrowConsumerFieldNotSet(consumerSettings, nameof(GooglePubSubConsumerBuilderExtensions.SubscriptionName));
        }
        base.AssertConsumer(consumerSettings);
    }

    protected override void AssertRequestResponseSettings()
    {
        base.AssertRequestResponseSettings();
        if (Settings.RequestResponse != null && string.IsNullOrWhiteSpace(Settings.RequestResponse.GetSubscriptionName()))
        {
            ThrowRequestResponseFieldNotSet(nameof(GooglePubSubConsumerBuilderExtensions.SubscriptionName));
        }
    }
}
