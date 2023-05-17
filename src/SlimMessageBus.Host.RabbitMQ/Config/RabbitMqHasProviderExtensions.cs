namespace SlimMessageBus.Host.RabbitMQ;

internal static class RabbitMqHasProviderExtensions
{
    public static RabbitMqMessageRoutingKeyProvider<object> GetMessageRoutingKeyProvider(this HasProviderExtensions p, HasProviderExtensions settings = null)
        => p.GetOrDefault<RabbitMqMessageRoutingKeyProvider<object>>(RabbitMqProperties.MessageRoutingKeyProvider, settings, null);

    public static RabbitMqMessagePropertiesModifier<object> GetMessagePropertiesModifier(this HasProviderExtensions p, HasProviderExtensions settings = null)
        => p.GetOrDefault<RabbitMqMessagePropertiesModifier<object>>(RabbitMqProperties.MessagePropertiesModifier, settings, null);

    public static string GetQueueName(this AbstractConsumerSettings p)
        => p.GetOrDefault<string>(RabbitMqProperties.QueueName, null);

    public static string GetQueueName(this RequestResponseSettings p)
        => p.GetOrDefault<string>(RabbitMqProperties.QueueName, null);

    public static string GetExchageType(this ProducerSettings p, HasProviderExtensions settings = null)
        => p.GetOrDefault<string>(RabbitMqProperties.ExchangeType, settings, null);
}
