namespace SlimMessageBus.Host.RabbitMQ;

public static class RabbitMqMessageBusSettingsExtensions
{
    /// <summary>
    /// Allows to attach an action that can perform additional RabbitMQ topology setup around exchanges and queues.
    /// </summary>
    /// <param name="settings"></param>
    /// <param name="action">Action to be executed, the first param is the RabbitMQ <see cref="IModel"/> from the underlying client, and second parameter represents the SMB exchange, queue and binding setup</param>
    public static RabbitMqMessageBusSettings UseTopologyInitializer(this RabbitMqMessageBusSettings settings, RabbitMqTopologyInitializer action)
    {
        if (settings is null) throw new ArgumentNullException(nameof(settings));
        if (action is null) throw new ArgumentNullException(nameof(action));

        RabbitMqProperties.TopologyInitializer.Set(settings, action);
        return settings;
    }

    /// <summary>
    /// Sets the default settings for exchanges on the bus level. This default will be taken unless it is overridden at the relevant producer level.
    /// </summary>
    /// <param name="settings"></param>
    /// <param name="durable"></param>
    /// <param name="autoDelete"></param>
    /// <returns></returns>
    public static RabbitMqMessageBusSettings UseExchangeDefaults(this RabbitMqMessageBusSettings settings, bool? durable = null, bool? autoDelete = null)
    {
        if (durable != null)
        {
            RabbitMqProperties.ExchangeDurable.Set(settings, durable.Value);
        }
        if (autoDelete != null)
        {
            RabbitMqProperties.ExchangeAutoDelete.Set(settings, autoDelete.Value);
        }
        return settings;
    }

    /// <summary>
    /// Sets the default settings for dead letter exchanges on the bus level. This default will be taken unless it is overridden at the relevant producer level.
    /// </summary>
    /// <param name="settings"></param>
    /// <param name="exchangeType">See <see cref="ExchangeType"/></param>
    /// <param name="durable"></param>
    /// <param name="autoDelete"></param>
    /// <param name="routingKey"></param>
    /// <returns></returns>
    public static RabbitMqMessageBusSettings UseDeadLetterExchangeDefaults(this RabbitMqMessageBusSettings settings, string exchangeType = null, bool? durable = null, bool? autoDelete = null, string routingKey = null)
    {
        if (exchangeType != null)
        {
            RabbitMqProperties.DeadLetterExchangeType.Set(settings, exchangeType);
        }
        if (durable != null)
        {
            RabbitMqProperties.DeadLetterExchangeDurable.Set(settings, durable.Value);
        }
        if (autoDelete != null)
        {
            RabbitMqProperties.DeadLetterExchangeAutoDelete.Set(settings, autoDelete.Value);
        }
        if (routingKey != null)
        {
            RabbitMqProperties.DeadLetterExchangeRoutingKey.Set(settings, routingKey);
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
        if (settings is null) throw new ArgumentNullException(nameof(settings));
        if (messagePropertiesModifier is null) throw new ArgumentNullException(nameof(messagePropertiesModifier));

        RabbitMqProperties.MessagePropertiesModifier.Set(settings, messagePropertiesModifier);
        return settings;
    }

    /// <summary>
    /// Enables RabbitMQ Publisher Confirms. When enabled, the channel will be created in confirm mode
    /// and each <c>BasicPublishAsync</c> call will wait for broker acknowledgement before completing.
    /// This provides guaranteed message delivery at the cost of reduced throughput.
    /// The default timeout of 10 seconds is used unless overridden via <see cref="RabbitMqMessageBusSettings.PublisherConfirmsTimeout"/>.
    /// </summary>
    /// <param name="settings"></param>
    /// <returns></returns>
    /// <remarks>
    /// See https://www.rabbitmq.com/docs/confirms#publisher-confirms for more information.
    /// </remarks>
    public static RabbitMqMessageBusSettings UsePublisherConfirms(this RabbitMqMessageBusSettings settings)
    {
        if (settings is null) throw new ArgumentNullException(nameof(settings));

        settings.EnablePublisherConfirms = true;
        return settings;
    }

    /// <summary>
    /// Enables RabbitMQ Publisher Confirms with a custom timeout.
    /// </summary>
    /// <param name="settings"></param>
    /// <param name="timeout">The timeout for waiting for broker acknowledgement. A <see cref="ProducerMessageBusException"/> is thrown if the broker does not respond within the given time.</param>
    /// <returns></returns>
    public static RabbitMqMessageBusSettings UsePublisherConfirms(this RabbitMqMessageBusSettings settings, TimeSpan timeout)
    {
        if (settings is null) throw new ArgumentNullException(nameof(settings));

        settings.EnablePublisherConfirms = true;
        settings.PublisherConfirmsTimeout = timeout;
        return settings;
    }

    /// <summary>
    /// Sets the default settings for queues on the bus level. This default will be taken unless it is overridden at the relevant consumer level.
    /// </summary>
    /// <param name="settings"></param>
    /// <param name="durable"></param>
    /// <param name="autoDelete"></param>
    /// <returns></returns>
    public static RabbitMqMessageBusSettings UseQueueDefaults(this RabbitMqMessageBusSettings settings, bool? durable = null, bool? autoDelete = null)
    {
        if (settings is null) throw new ArgumentNullException(nameof(settings));

        if (durable != null)
        {
            RabbitMqProperties.QueueDurable.Set(settings, durable.Value);
        }
        if (autoDelete != null)
        {
            RabbitMqProperties.QueueAutoDelete.Set(settings, autoDelete.Value);
        }
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
        if (settings is null) throw new ArgumentNullException(nameof(settings));
        if (routingKeyProvider is null) throw new ArgumentNullException(nameof(routingKeyProvider));

        RabbitMqProperties.MessageRoutingKeyProvider.Set(settings, routingKeyProvider);
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
        if (settings is null) throw new ArgumentNullException(nameof(settings));

        RabbitMqProperties.MessageAcknowledgementMode.Set(settings, mode);
        return settings;
    }
}

