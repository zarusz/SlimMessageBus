namespace SlimMessageBus.Host.RabbitMQ;

public static class RabbitMqProducerBuilderExtensions
{
    /// <summary>
    /// Sets the exchange name and optionally other parameters.
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
        if (exchangeType != null)
        {
            builder.ExchangeType(exchangeType.Value);
        }
        if (durable != null)
        {
            builder.Durable(durable.Value);
        }
        if (autoDelete != null)
        {
            builder.AutoDelete(autoDelete.Value);
        }
        if (arguments != null)
        {
            builder.ExchangeArguments(arguments);
        }
        return builder;
    }

    /// <summary>
    /// Sets the exchange type.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder"></param>
    /// <param name="exchangeType"></param>
    /// <returns></returns>
    public static ProducerBuilder<T> ExchangeType<T>(this ProducerBuilder<T> builder, ExchangeType exchangeType)
    {
        builder.Settings.Properties[RabbitMqProperties.ExchangeType] = MapExchangeType(exchangeType);
        return builder;
    }

    /// <summary>
    /// Sets the the exchange to durable or not.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder"></param>
    /// <param name="enabled"></param>
    /// <returns></returns>
    public static ProducerBuilder<T> Durable<T>(this ProducerBuilder<T> builder, bool enabled = true)
    {
        builder.Settings.Properties[RabbitMqProperties.ExchangeDurable] = enabled;
        return builder;
    }

    /// <summary>
    /// Sets the auto delete on an exchange.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder"></param>
    /// <param name="enabled"></param>
    /// <returns></returns>
    public static ProducerBuilder<T> AutoDelete<T>(this ProducerBuilder<T> builder, bool enabled = true)
    {
        builder.Settings.Properties[RabbitMqProperties.ExchangeAutoDelete] = enabled;
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

    /// <summary>
    /// Sets the exchange arguments.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder"></param>
    /// <param name="exchangeArguments"></param>
    /// <returns></returns>
    public static ProducerBuilder<T> ExchangeArguments<T>(this ProducerBuilder<T> builder, IDictionary<string, object> exchangeArguments)
    {
        builder.Settings.Properties[RabbitMqProperties.ExchangeArguments] = exchangeArguments;
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

/// <summary>
/// Represents an initializer that is able to perform additional RabbitMQ topology setup.
/// </summary>
/// <param name="channel">The RabbitMQ client channel</param>
/// <param name="applyDefaultTopology">Calling this action will peform the default topology setup by SMB</param>
public delegate void RabbitMqTopologyInitializer(IModel channel, Action applyDefaultTopology);

/// <summary>
/// Represents a key provider provider for a given message.
/// </summary>
/// <typeparam name="T"></typeparam>
/// <param name="message">The message</param>
/// <param name="messageProperties">The message properties</param>
/// <returns></returns>
public delegate string RabbitMqMessageRoutingKeyProvider<T>(T message, IBasicProperties messageProperties);

/// <summary>
/// Represents a message modifier for a given message.
/// </summary>
/// <typeparam name="T"></typeparam>
/// <param name="message">The message</param>
/// <param name="messageProperties">The message properties to be modified</param>
/// <returns></returns>
public delegate void RabbitMqMessagePropertiesModifier<T>(T message, IBasicProperties messageProperties);

