namespace SlimMessageBus.Host.RabbitMQ;

static internal class RabbitMqHasProviderExtensions
{
    public static RabbitMqMessageRoutingKeyProvider<object> GetMessageRoutingKeyProvider(this HasProviderExtensions p, HasProviderExtensions settings = null)
        => p.GetOrDefault<RabbitMqMessageRoutingKeyProvider<object>>(RabbitMqProperties.MessageRoutingKeyProvider, settings, null);

    public static RabbitMqMessagePropertiesModifier<object> GetMessagePropertiesModifier(this HasProviderExtensions p, HasProviderExtensions settings = null)
        => p.GetOrDefault<RabbitMqMessagePropertiesModifier<object>>(RabbitMqProperties.MessagePropertiesModifier, settings, null);

    public static string GetQueueName(this AbstractConsumerSettings c)
        => c.GetOrDefault<string>(RabbitMqProperties.QueueName, null);

    public static string GetBindingRoutingKey(this AbstractConsumerSettings c, HasProviderExtensions settings = null)
        => c.GetOrDefault<string>(RabbitMqProperties.BindingRoutingKey, settings, null);

    public static string GetExchangeType(this ProducerSettings p, HasProviderExtensions settings = null)
        => p.GetOrDefault<string>(RabbitMqProperties.ExchangeType, settings, null);

    /// <summary>
    /// Resolves whether publisher confirms are enabled for the given producer.
    /// Producer-level setting takes precedence over bus-level setting.
    /// </summary>
    public static bool IsPublisherConfirmsEnabled(this ProducerSettings p, RabbitMqMessageBusSettings providerSettings)
        => p.GetOrDefault<bool?>(RabbitMqProperties.EnablePublisherConfirms, null) ?? providerSettings.EnablePublisherConfirms;
}
