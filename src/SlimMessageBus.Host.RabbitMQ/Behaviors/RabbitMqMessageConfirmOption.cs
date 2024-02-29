namespace SlimMessageBus.Host.RabbitMQ;

[Flags]
public enum RabbitMqMessageConfirmOption
{
    Ack = 1,
    Nack = 2,
    Requeue = 4
}
