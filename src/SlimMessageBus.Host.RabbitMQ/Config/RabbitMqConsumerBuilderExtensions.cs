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

        RabbitMqProperties.QueueName.Set(builder.ConsumerSettings, queueName);

        if (exclusive != null)
        {
            RabbitMqProperties.QueueExclusive.Set(builder.ConsumerSettings, exclusive.Value);
        }

        if (durable != null)
        {
            RabbitMqProperties.QueueDurable.Set(builder.ConsumerSettings, durable.Value);
        }

        if (autoDelete != null)
        {
            RabbitMqProperties.QueueAutoDelete.Set(builder.ConsumerSettings, autoDelete.Value);
        }

        if (arguments != null)
        {
            RabbitMqProperties.QueueArguments.Set(builder.ConsumerSettings, arguments);
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
        RabbitMqProperties.BindingRoutingKey.Set(builder.ConsumerSettings, routingKey);

        return builder;
    }

    /// <summary>
    /// Sets the exchange name that will act as the dead letter (it will set the "x-dead-letter-exchange" attribute on the queue).
    /// </summary>
    /// <remarks>See https://www.rabbitmq.com/dlx.html</remarks>
    /// <typeparam name="TConsumerBuilder"></typeparam>
    /// <param name="builder"></param>
    /// <param name="exchangeName">Will set the "x-dead-letter-exchange" argument on the queue</param>
    /// <param name="exchangeType">Type of the exchange, when provided SMB will attempt to provision the exchange. See <see cref="ExchangeType"/>.</param>
    /// <param name="durable"></param>
    /// <param name="autoDelete"></param>
    /// <param name="routingKey">Will set the "x-dead-letter-routing-key" argument on the queue</param>
    /// <returns></returns>
    public static TConsumerBuilder DeadLetterExchange<TConsumerBuilder>(this TConsumerBuilder builder, string exchangeName, string exchangeType = null, bool? durable = null, bool? autoDelete = null, string routingKey = null)
        where TConsumerBuilder : AbstractConsumerBuilder
    {
        if (string.IsNullOrEmpty(exchangeName)) throw new ArgumentNullException(nameof(exchangeName));

        RabbitMqProperties.DeadLetterExchange.Set(builder.ConsumerSettings, exchangeName);

        if (exchangeType != null)
        {
            RabbitMqProperties.DeadLetterExchangeType.Set(builder.ConsumerSettings, exchangeType);
        }

        if (durable != null)
        {
            RabbitMqProperties.DeadLetterExchangeDurable.Set(builder.ConsumerSettings, durable.Value);
        }

        if (autoDelete != null)
        {
            RabbitMqProperties.DeadLetterExchangeAutoDelete.Set(builder.ConsumerSettings, autoDelete.Value);
        }

        if (routingKey != null)
        {
            RabbitMqProperties.DeadLetterExchangeRoutingKey.Set(builder.ConsumerSettings, routingKey);
        }

        return builder;
    }

    /// <summary>
    /// Sets the message Acknowledgement Mode for the consumer.
    /// </summary>
    /// <typeparam name="TConsumerBuilder"></typeparam>
    /// <param name="builder"></param>
    /// <param name="mode"></param>
    /// <returns></returns>
    public static TConsumerBuilder AcknowledgementMode<TConsumerBuilder>(this TConsumerBuilder builder, RabbitMqMessageAcknowledgementMode mode)
       where TConsumerBuilder : AbstractConsumerBuilder
    {
        RabbitMqProperties.MessageAcknowledgementMode.Set(builder.ConsumerSettings, mode);
        return builder;
    }
}