namespace SlimMessageBus.Host.RabbitMQ;

public static class RabbitMqProducerBuilderExtensions
{
    /// <summary>
    /// Sets the exchange name (and optionally other exchange parameters).
    /// </summary>
    /// <remarks>Setting the name is equivalent to using <see cref="ProducerBuilder{T}.DefaultPath(string)"/>.</remarks>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder"></param>
    /// <param name="exchangeName"></param>
    /// <param name="exchangeType"></param>
    /// <param name="durable"></param>
    /// <param name="autoDelete"></param>
    /// <param name="arguments"></param>
    /// <returns></returns>
    public static ProducerBuilder<T> Exchange<T>(this ProducerBuilder<T> builder, string exchangeName, ExchangeType? exchangeType = null, bool? durable = null, bool? autoDelete = null, IDictionary<string, object>? arguments = null)
    {
        if (string.IsNullOrEmpty(exchangeName)) throw new ArgumentNullException(nameof(exchangeName));

        builder.DefaultPath(exchangeName);
        builder.Settings.SetExchangeProperties(exchangeType: exchangeType, durable: durable, autoDelete: autoDelete, arguments: arguments);
        return builder;
    }

    internal static void SetExchangeProperties(this HasProviderExtensions settings, ExchangeType? exchangeType = null, bool? durable = null, bool? autoDelete = null, IDictionary<string, object>? arguments = null)
    {
        if (exchangeType != null)
        {
            settings.Properties[RabbitMqProperties.ExchangeType] = MapExchangeType(exchangeType.Value);
        }
        if (durable != null)
        {
            settings.Properties[RabbitMqProperties.ExchangeDurable] = durable.Value;
        }
        if (autoDelete != null)
        {
            settings.Properties[RabbitMqProperties.ExchangeAutoDelete] = autoDelete.Value;
        }
        if (arguments != null)
        {
            settings.Properties[RabbitMqProperties.ExchangeArguments] = arguments;
        }
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

        RabbitMqMessageRoutingKeyProvider<object> typed = (object message, IBasicProperties properties) =>
        {
            if (message is T typedMessage)
            {
                return routingKeyProvider(typedMessage, properties);
            }
            return default;
        };
        builder.Settings.Properties[RabbitMqProperties.MessageRoutingKeyProvider] = typed;
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

        RabbitMqMessagePropertiesModifier<object> typed = (object message, IBasicProperties properties) =>
        {
            if (message is T typedMessage)
            {
                messagePropertiesModifier(typedMessage, properties);
            }
        };
        builder.Settings.Properties[RabbitMqProperties.MessagePropertiesModifier] = typed;
        return builder;
    }

    internal static string MapExchangeType(ExchangeType exchangeType) => exchangeType switch
    {
        RabbitMQ.ExchangeType.Direct => global::RabbitMQ.Client.ExchangeType.Direct,
        RabbitMQ.ExchangeType.Fanout => global::RabbitMQ.Client.ExchangeType.Fanout,
        RabbitMQ.ExchangeType.Headers => global::RabbitMQ.Client.ExchangeType.Headers,
        RabbitMQ.ExchangeType.Topic => global::RabbitMQ.Client.ExchangeType.Topic,
        _ => throw new ArgumentOutOfRangeException(nameof(exchangeType))
    };
}
