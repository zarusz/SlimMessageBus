namespace SlimMessageBus.Host.RabbitMQ;

public static class RabbitMqProducerBuilderExtensions
{
    /// <summary>
    /// Enables or disables RabbitMQ Publisher Confirms for this producer.
    /// When enabled, publish operations will wait for broker acknowledgement before completing.
    /// This overrides the bus-level <see cref="RabbitMqMessageBusSettings.EnablePublisherConfirms"/> setting.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder"></param>
    /// <param name="enabled">Whether to enable publisher confirms for this producer. Default is <c>true</c>.</param>
    /// <returns></returns>
    public static ProducerBuilder<T> EnablePublisherConfirms<T>(this ProducerBuilder<T> builder, bool enabled = true)
    {
        RabbitMqProperties.EnablePublisherConfirms.Set(builder.Settings, enabled);
        return builder;
    }

    /// <summary>
    /// Sets the exchange name (and optionally other exchange parameters).
    /// </summary>
    /// <remarks>Setting the name is equivalent to using <see cref="ProducerBuilder{T}.DefaultPath(string)"/>.</remarks>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder"></param>
    /// <param name="exchangeName"></param>
    /// <param name="exchangeType">See <see cref="ExchangeType"/></param>
    /// <param name="durable"></param>
    /// <param name="autoDelete"></param>
    /// <param name="arguments"></param>
    /// <returns></returns>
    public static ProducerBuilder<T> Exchange<T>(this ProducerBuilder<T> builder, string exchangeName, string exchangeType = null, bool? durable = null, bool? autoDelete = null, IDictionary<string, object> arguments = null)
    {
        if (string.IsNullOrEmpty(exchangeName)) throw new ArgumentNullException(nameof(exchangeName));

        builder.DefaultPath(exchangeName);
        builder.Settings.SetExchangeProperties(exchangeType: exchangeType, durable: durable, autoDelete: autoDelete, arguments: arguments);
        return builder;
    }

    /// <summary>
    /// Sets an modifier action for the message properties (headers). The content type, message id, and other RabbitMQ message properties can be set that way.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder"></param>
    /// <param name="messagePropertiesModifier"></param>
    /// <returns></returns>
    public static ProducerBuilder<T> MessagePropertiesModifier<T>(this ProducerBuilder<T> builder, RabbitMqMessagePropertiesModifier<T> messagePropertiesModifier)
    {
        if (messagePropertiesModifier == null) throw new ArgumentNullException(nameof(messagePropertiesModifier));

        RabbitMqMessagePropertiesModifier<object> untyped = (object message, IBasicProperties properties) =>
        {
            if (message is T typedMessage)
            {
                messagePropertiesModifier(typedMessage, properties);
            }
        };
        RabbitMqProperties.MessagePropertiesModifier.Set(builder.Settings, untyped);
        return builder;
    }

    /// <summary>
    /// Sets the provider for the Routing Key for a message
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder"></param>
    /// <param name="routingKeyProvider"></param>
    /// <returns></returns>
    public static ProducerBuilder<T> RoutingKeyProvider<T>(this ProducerBuilder<T> builder, RabbitMqMessageRoutingKeyProvider<T> routingKeyProvider)
    {
        if (routingKeyProvider == null) throw new ArgumentNullException(nameof(routingKeyProvider));

        RabbitMqMessageRoutingKeyProvider<object> untyped = (object message, IBasicProperties properties) =>
        {
            if (message is T typedMessage)
            {
                return routingKeyProvider(typedMessage, properties);
            }
            return default;
        };
        RabbitMqProperties.MessageRoutingKeyProvider.Set(builder.Settings, untyped);
        return builder;
    }

    static internal void SetExchangeProperties(this HasProviderExtensions settings, string exchangeType = null, bool? durable = null, bool? autoDelete = null, IDictionary<string, object> arguments = null)
    {
        if (exchangeType != null)
        {
            RabbitMqProperties.ExchangeType.Set(settings, exchangeType);
        }
        if (durable != null)
        {
            RabbitMqProperties.ExchangeDurable.Set(settings, durable.Value);
        }
        if (autoDelete != null)
        {
            RabbitMqProperties.ExchangeAutoDelete.Set(settings, autoDelete.Value);
        }
        if (arguments != null)
        {
            RabbitMqProperties.ExchangeArguments.Set(settings, arguments);
        }
    }
}
