namespace SlimMessageBus.Host.RabbitMQ;

public static class RabbitMqMessageBusSettingsExtensions
{
    /// <summary>
    /// Allows to attach an action that can perform additional RabbitMQ topology setup around exchanges and queues.
    /// </summary>
    /// <param name="settings"></param>
    /// <param name="action">Action to be executed, the first param is the RabbitMQ <see cref="IModel"/> from the underlying client, and second parameter represents the SMB exchange, queue and binding setup</param>
    public static RabbitMqMessageBusSettings UseTopologyInitalizer(this RabbitMqMessageBusSettings settings, RabbitMqTopologyInitializer action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));

        settings.Properties[RabbitMqProperties.TopologyInitializer] = action;
        return settings;
    }

    /// <summary>
    /// Sets the default settings for exchanges on the bus level. This default will be taken unless it is overriden at the relevant producer level.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="settings"></param>
    /// <param name="durable"></param>
    /// <param name="autoDelete"></param>
    /// <returns></returns>
    public static RabbitMqMessageBusSettings UseExchangeDefaults(this RabbitMqMessageBusSettings settings, bool? durable = null, bool? autoDelete = null)
    {
        if (durable != null)
        {
            settings.Properties[RabbitMqProperties.ExchangeDurable] = durable.Value;
        }
        if (autoDelete != null)
        {
            settings.Properties[RabbitMqProperties.ExchangeAutoDelete] = autoDelete.Value;
        }
        return settings;
    }

    /// <summary>
    /// Sets the default settings for dead letter exchanges on the bus level. This default will be taken unless it is overriden at the relevant producer level.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="settings"></param>
    /// <param name="exchangeType"></param>
    /// <param name="durable"></param>
    /// <param name="autoDelete"></param>
    /// <param name="routingKey"></param>
    /// <returns></returns>
    public static RabbitMqMessageBusSettings UseDeadLetterExchangeDefaults(this RabbitMqMessageBusSettings settings, ExchangeType? exchangeType = null, bool ? durable = null, bool? autoDelete = null, string routingKey = null)
    {
        if (exchangeType != null)
        {
            settings.Properties[RabbitMqProperties.DeadLetterExchangeType] = RabbitMqProducerBuilderExtensions.MapExchangeType(exchangeType.Value);
        }
        if (durable != null)
        {
            settings.Properties[RabbitMqProperties.DeadLetterExchangeDurable] = durable.Value;
        }
        if (autoDelete != null)
        {
            settings.Properties[RabbitMqProperties.DeadLetterExchangeAutoDelete] = autoDelete.Value;
        }
        if (routingKey != null)
        {
            settings.Properties[RabbitMqProperties.DeadLetterExchangeRoutingKey] = routingKey;
        }
        return settings;
    }

    /// <summary>
    /// Sets the default settings for queues on the bus level. This default will be taken unless it is overriden at the relevant consumer level.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="settings"></param>
    /// <param name="durable"></param>
    /// <param name="autoDelete"></param>
    /// <returns></returns>
    public static RabbitMqMessageBusSettings UseQueueDefaults(this RabbitMqMessageBusSettings settings, bool? durable = null, bool? autoDelete = null)
    {
        if (durable != null)
        {
            settings.Properties[RabbitMqProperties.QueueDurable] = durable.Value;
        }
        if (autoDelete != null)
        {
            settings.Properties[RabbitMqProperties.QueueAutoDelete] = autoDelete.Value;
        }
        return settings;
    }

    /// <summary>
    /// Sets an modifier action for the message properties (headers) on the bus level. The content type, message id, and other RabbitMQ message properties can be set that way.
    /// </summary>
    /// <param name="settings"></param>
    /// <param name="messagePropertiesModifier"></param>
    /// <returns></returns>
    public static RabbitMqMessageBusSettings UseMessagePropertiesModifier(this RabbitMqMessageBusSettings settings, RabbitMqMessagePropertiesModifier<object> messagePropertiesModifier)
    {
        if (messagePropertiesModifier == null) throw new ArgumentNullException(nameof(messagePropertiesModifier));

        settings.Properties[RabbitMqProperties.MessagePropertiesModifier] = messagePropertiesModifier;
        return settings;
    }

    /// <summary>
    /// Sets the provider for the Routing Key for a message on the bus level
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder"></param>
    /// <param name="routingKeyProvider"></param>
    /// <returns></returns>
    public static RabbitMqMessageBusSettings UseRoutingKeyProvider(this RabbitMqMessageBusSettings settings, RabbitMqMessageRoutingKeyProvider<object> routingKeyProvider)
    {
        if (routingKeyProvider == null) throw new ArgumentNullException(nameof(routingKeyProvider));

        settings.Properties[RabbitMqProperties.MessageRoutingKeyProvider] = routingKeyProvider;
        return settings;
    }
}

