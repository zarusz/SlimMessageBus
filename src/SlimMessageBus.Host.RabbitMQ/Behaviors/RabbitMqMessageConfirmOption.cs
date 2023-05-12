namespace SlimMessageBus.Host.RabbitMQ;

[Flags]
internal enum RabbitMqMessageConfirmOption
{
    Ack = 1,
    Nack = 2,
    Requeue = 4
}
