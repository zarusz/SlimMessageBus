namespace SlimMessageBus.Host.RabbitMQ;

using SlimMessageBus.Host.Services;

internal class RabbitMqMessageBusSettingsValidationService : DefaultMessageBusSettingsValidationService<RabbitMqMessageBusSettings>
{
    public RabbitMqMessageBusSettingsValidationService(MessageBusSettings settings, RabbitMqMessageBusSettings providerSettings) : base(settings, providerSettings)
    {
    }

    public override void AssertSettings()
    {
        base.AssertSettings();

        if (ProviderSettings.ConnectionFactory is null)
        {
            ThrowFieldNotSet($"{nameof(RabbitMqMessageBusSettings)}.{nameof(RabbitMqMessageBusSettings.ConnectionFactory)}");
        }
    }

    protected override void AssertProducer(ProducerSettings producerSettings)
    {
        base.AssertProducer(producerSettings);

        var exchangeName = producerSettings.DefaultPath;
        if (exchangeName == null)
        {
            ThrowProducerFieldNotSet(producerSettings, nameof(RabbitMqProducerBuilderExtensions.Exchange));
        }

        var routingKeyProvider = producerSettings.GetMessageRoutingKeyProvider(ProviderSettings);
        if (routingKeyProvider == null)
        {
            // Requirement for the routing key depends on the ExchangeType
            var exchangeType = producerSettings.GetExchageType(ProviderSettings);
            if (exchangeType == global::RabbitMQ.Client.ExchangeType.Direct || exchangeType == global::RabbitMQ.Client.ExchangeType.Topic)
            {
                ThrowProducerFieldNotSet(producerSettings, nameof(RabbitMqProducerBuilderExtensions.RoutingKeyProvider), $"is neither provided on the producer for exchange {producerSettings.DefaultPath} nor a default provider exists at the bus level (check that .{nameof(RabbitMqProducerBuilderExtensions.RoutingKeyProvider)}() exists on the producer or bus level). Exchange type {exchangeType} requires the producer to has a routing key provider.");
            }
        }
    }

    protected override void AssertConsumer(ConsumerSettings consumerSettings)
    {
        base.AssertConsumer(consumerSettings);

        var exchangeName = consumerSettings.Path;
        if (exchangeName == null)
        {
            ThrowConsumerFieldNotSet(consumerSettings, nameof(RabbitMqConsumerBuilderExtensions.ExchangeBinding));
        }

        var queueName = consumerSettings.GetQueueName();
        if (queueName == null)
        {
            ThrowConsumerFieldNotSet(consumerSettings, nameof(RabbitMqConsumerBuilderExtensions.Queue));
        }
    }

    protected override void AssertRequestResponseSettings()
    {
        base.AssertRequestResponseSettings();

        if (Settings.RequestResponse != null)
        {
            var exchangeName = Settings.RequestResponse.Path;
            if (exchangeName == null)
            {
                ThrowRequestResponseFieldNotSet(nameof(RabbitMqRequestResponseBuilderExtensions.ReplyToExchange));
            }

            var queueName = Settings.RequestResponse.GetQueueName();
            if (queueName == null)
            {
                ThrowRequestResponseFieldNotSet(nameof(RabbitMqConsumerBuilderExtensions.Queue));
            }
        }

    }
}
