namespace SlimMessageBus.Host.RabbitMQ;

using System;

public static class RabbitMqMessageBusSettingsExtensions
{
    /// <summary>
    /// Allows to attach an action that can perform additional RabbitMQ topology setup around exchanges and queues.
    /// </summary>
    /// <param name="settings"></param>
    /// <param name="action">Action to be executed, the first param is the RabbitMQ <see cref="IModel"/> from the underlying client, and second parameter represents the SMB exchange, queue and binding setup</param>
    public static RabbitMqMessageBusSettings UseTopologyInitializer(this RabbitMqMessageBusSettings settings, RabbitMqTopologyInitializer action)
    {
#if NETSTANDARD2_0
        if (settings is null) throw new ArgumentNullException(nameof(settings));
        if (action is null) throw new ArgumentNullException(nameof(action));
#else
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(action);
#endif

        settings.Properties[RabbitMqProperties.TopologyInitializer] = action;
        return settings;
    }

    /// <summary>
    /// Sets the default settings for exchanges on the bus level. This default will be taken unless it is overriden at the relevant producer level.
    /// </summary>
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
    /// <param name="settings"></param>
    /// <param name="exchangeType"></param>
    /// <param name="durable"></param>
    /// <param name="autoDelete"></param>
    /// <param name="routingKey"></param>
    /// <returns></returns>
    public static RabbitMqMessageBusSettings UseDeadLetterExchangeDefaults(this RabbitMqMessageBusSettings settings, ExchangeType? exchangeType = null, bool? durable = null, bool? autoDelete = null, string routingKey = null)
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
    /// <param name="settings"></param>
    /// <param name="durable"></param>
    /// <param name="autoDelete"></param>
    /// <returns></returns>
    public static RabbitMqMessageBusSettings UseQueueDefaults(this RabbitMqMessageBusSettings settings, bool? durable = null, bool? autoDelete = null)
    {
#if NETSTANDARD2_0
        if (settings is null) throw new ArgumentNullException(nameof(settings));
#else
        ArgumentNullException.ThrowIfNull(settings);
#endif

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
#if NETSTANDARD2_0
        if (settings is null) throw new ArgumentNullException(nameof(settings));
        if (messagePropertiesModifier is null) throw new ArgumentNullException(nameof(messagePropertiesModifier));
#else
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(messagePropertiesModifier);
#endif

        settings.Properties[RabbitMqProperties.MessagePropertiesModifier] = messagePropertiesModifier;
        return settings;
    }

    /// <summary>
    /// Sets the provider for the Routing Key for a message on the bus level
    /// </summary>
    /// <param name="settings"></param>
    /// <param name="routingKeyProvider"></param>
    /// <returns></returns>
    public static RabbitMqMessageBusSettings UseRoutingKeyProvider(this RabbitMqMessageBusSettings settings, RabbitMqMessageRoutingKeyProvider<object> routingKeyProvider)
    {
#if NETSTANDARD2_0
        if (settings is null) throw new ArgumentNullException(nameof(settings));
        if (routingKeyProvider is null) throw new ArgumentNullException(nameof(routingKeyProvider));
#else
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(routingKeyProvider);
#endif

        settings.Properties[RabbitMqProperties.MessageRoutingKeyProvider] = routingKeyProvider;
        return settings;
    }

    /// <summary>
    /// Sets the default message Acknowledgement Mode for the message on the bus level.
    /// </summary>
    /// <param name="settings"></param>
    /// <param name="mode"></param>
    /// <returns></returns>
    public static RabbitMqMessageBusSettings AcknowledgementMode(this RabbitMqMessageBusSettings settings, RabbitMqMessageAcknowledgementMode mode)
    {
#if NETSTANDARD2_0
        if (settings is null) throw new ArgumentNullException(nameof(settings));
#else
        ArgumentNullException.ThrowIfNull(settings);
#endif

        settings.Properties[RabbitMqProperties.MessageAcknowledgementMode] = mode;
        return settings;
    }
}

