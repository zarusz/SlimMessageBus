namespace SlimMessageBus.Host.RabbitMQ;

public enum ExchangeType
{
    /// <summary>
    /// Exchange type used for AMQP direct exchanges.
    /// </summary>
    Direct = 0,

    /// <summary>
    /// Exchange type used for AMQP fanout exchanges.
    /// </summary>
    Fanout = 1,

    /// <summary>
    /// Exchange type used for AMQP headers exchanges.
    /// </summary>
    Headers = 2,

    /// <summary>
    /// Exchange type used for AMQP topic exchanges.
    /// </summary>
    Topic = 3
}
