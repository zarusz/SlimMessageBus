namespace SlimMessageBus.Host.RabbitMQ;

[Flags]
public enum RabbitMqMessageConfirmOptions
{
    Ack = 1,
    Nack = 2,
    Requeue = 4
}
