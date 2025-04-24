namespace SlimMessageBus.Host.RabbitMQ;

public static class RabbitMqRequestResponseBuilderExtensions
{
    /// <summary>
    /// Sets the exchange name (and optionally other exchange parameters) should the handler send the responses to.
    /// </summary>
    /// <remarks>Setting the name is equivalent to using <see cref="RequestResponseBuilder.DefaultPath(string)"/>.</remarks>
    /// <param name="builder"></param>
    /// <param name="exchangeName"></param>
    /// <param name="exchangeType">See <see cref="ExchangeType"/></param>
    /// <param name="durable"></param>
    /// <param name="autoDelete"></param>
    /// <param name="arguments"></param>
    /// <returns></returns>
    public static RequestResponseBuilder ReplyToExchange(this RequestResponseBuilder builder, string exchangeName, string exchangeType = null, bool? durable = null, bool? autoDelete = null, IDictionary<string, object> arguments = null)
    {
        if (string.IsNullOrEmpty(exchangeName)) throw new ArgumentNullException(nameof(exchangeName));

        builder.ReplyToPath(exchangeName);
        builder.Settings.SetExchangeProperties(exchangeType: exchangeType, durable: durable, autoDelete: autoDelete, arguments: arguments);
        return builder;
    }

    /// <summary>
    /// Sets the binding of this request-response <see cref="Queue(RequestResponseBuilder, string, bool?, bool?, bool?, IDictionary{string, object})"/> to the <see cref="ReplyToExchange(RequestResponseBuilder, string, ExchangeType?, bool?, bool?, IDictionary{string, object}?)"/> (will declare at the bus initialization).
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="routingKey"></param>
    /// <returns></returns>
    public static RequestResponseBuilder ExchangeBinding(this RequestResponseBuilder builder, string routingKey = "")
    {
        RabbitMqProperties.BindingRoutingKey.Set(builder.Settings, routingKey);
        return builder;
    }
}

