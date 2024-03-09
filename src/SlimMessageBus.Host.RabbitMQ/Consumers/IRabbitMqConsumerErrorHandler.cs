namespace SlimMessageBus.Host.RabbitMQ;

public interface IRabbitMqConsumerErrorHandler<in T> : IConsumerErrorHandler<T>
{
}