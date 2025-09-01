namespace SlimMessageBus.Host.RabbitMQ;

public static class ExchangeType
{
    /// <summary>
    /// Exchange type used for AMQP direct exchanges.
    /// </summary>
    public static readonly string Direct = global::RabbitMQ.Client.ExchangeType.Direct;

    /// <summary>
    /// Exchange type used for AMQP fanout exchanges.
    /// </summary>
    public static readonly string Fanout = global::RabbitMQ.Client.ExchangeType.Fanout;

    /// <summary>
    /// Exchange type used for AMQP headers exchanges.
    /// </summary>
    public static readonly string Headers = global::RabbitMQ.Client.ExchangeType.Headers;

    /// <summary>
    /// Exchange type used for AMQP topic exchanges.
    /// </summary>
    public static readonly string Topic = global::RabbitMQ.Client.ExchangeType.Topic;

    /// <summary>
    /// Exchange type used for Delayed Message Plugin exchanges. 
    /// See more: <see href="https://www.rabbitmq.com/blog/2015/04/16/scheduling-messages-with-rabbitmq#installing-the-plugin"/>
    /// </summary>
    public static readonly string XDelayedMessage = "x-delayed-message";
}
