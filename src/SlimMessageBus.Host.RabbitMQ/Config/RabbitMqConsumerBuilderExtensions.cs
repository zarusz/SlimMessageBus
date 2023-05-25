namespace SlimMessageBus.Host.RabbitMQ;

public static class RabbitMqConsumerBuilderExtensions
{
    /// <summary>
    /// Sets the queue name (and optionally other parameters) for a RabbitMQ queue.
    /// </summary>
    /// <typeparam name="TConsumerBuilder"></typeparam>
    /// <param name="builder"></param>
    /// <param name="queueName"></param>
    /// <param name="durable"></param>
    /// <param name="autoDelete"></param>
    /// <param name="exclusive"></param>
    /// <param name="arguments"></param>
    /// <returns></returns>
    public static TConsumerBuilder Queue<TConsumerBuilder>(this TConsumerBuilder builder, string queueName, bool? exclusive = null, bool? durable = null, bool? autoDelete = null, IDictionary<string, object> arguments = null)
        where TConsumerBuilder : IAbstractConsumerBuilder
    {
        if (string.IsNullOrEmpty(queueName)) throw new ArgumentNullException(nameof(queueName));

        builder.ConsumerSettings.Properties[RabbitMqProperties.QueueName] = queueName;

        if (exclusive != null)
        {
            builder.ConsumerSettings.Properties[RabbitMqProperties.QueueExclusive] = exclusive.Value;
        }

        if (durable != null)
        {
            builder.ConsumerSettings.Properties[RabbitMqProperties.QueueDurable] = durable.Value;
        }

        if (autoDelete != null)
        {
            builder.ConsumerSettings.Properties[RabbitMqProperties.QueueAutoDelete] = autoDelete.Value;
        }

        if (arguments != null)
        {
            builder.ConsumerSettings.Properties[RabbitMqProperties.QueueArguments] = arguments;
        }

        return builder;
    }

    /// <summary>
    /// Sets the binding of this consumer queue to an exchange (will declare at the bus initialization).
    /// </summary>
    /// <remarks>The exchange name is set using <see cref="ConsumerBuilder{T}.Path(string)"/>.</remarks>
    /// <typeparam name="TConsumerBuilder"></typeparam>
    /// <param name="builder"></param>
    /// <param name="exchangeName"></param>
    /// <param name="routingKey"></param>
    /// <returns></returns>
    public static TConsumerBuilder ExchangeBinding<TConsumerBuilder>(this TConsumerBuilder builder, string exchangeName, string routingKey = "")
        where TConsumerBuilder : AbstractConsumerBuilder
    {
        if (string.IsNullOrEmpty(exchangeName)) throw new ArgumentNullException(nameof(exchangeName));

        builder.ConsumerSettings.Path = exchangeName;
        builder.ConsumerSettings.Properties[RabbitMqProperties.BindingRoutingKey] = routingKey;

        return builder;
    }

    /// <summary>
    /// Sets the exchange name that will act as the dead letter (it will set the "x-dead-letter-exchange" attribute on the queue).
    /// </summary>
    /// <remarks>See https://www.rabbitmq.com/dlx.html</remarks>
    /// <typeparam name="TConsumerBuilder"></typeparam>
    /// <param name="builder"></param>
    /// <param name="exchangeName">Will set the "x-dead-letter-exchange" argument on the queue</param>
    /// <param name="exchangeType">Type of the exchange, when provided SMB will attempt to provision the exchange</param>
    /// <param name="durable"></param>
    /// <param name="autoDelete"></param>
    /// <param name="routingKey">Will set the "x-dead-letter-routing-key" argument on the queue</param>
    /// <returns></returns>
    public static TConsumerBuilder DeadLetterExchange<TConsumerBuilder>(this TConsumerBuilder builder, string exchangeName, ExchangeType? exchangeType = null, bool? durable = null, bool? autoDelete = null, string routingKey = null)
        where TConsumerBuilder : AbstractConsumerBuilder
    {
        if (string.IsNullOrEmpty(exchangeName)) throw new ArgumentNullException(nameof(exchangeName));

        builder.ConsumerSettings.Properties[RabbitMqProperties.DeadLetterExchange] = exchangeName;

        if (exchangeType != null)
        {
            builder.ConsumerSettings.Properties[RabbitMqProperties.DeadLetterExchangeType] = RabbitMqProducerBuilderExtensions.MapExchangeType(exchangeType.Value);
        }

        if (durable != null)
        {
            builder.ConsumerSettings.Properties[RabbitMqProperties.DeadLetterExchangeDurable] = durable.Value;
        }

        if (autoDelete != null)
        {
            builder.ConsumerSettings.Properties[RabbitMqProperties.DeadLetterExchangeAutoDelete] = autoDelete.Value;
        }

        if (routingKey != null)
        {
            builder.ConsumerSettings.Properties[RabbitMqProperties.DeadLetterExchangeRoutingKey] = routingKey;
        }

        return builder;
    }
}

