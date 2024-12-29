namespace SlimMessageBus.Host.RabbitMQ;

public interface IRabbitMqConsumerErrorHandler<in T> : IConsumerErrorHandler<T>;

public abstract class RabbitMqConsumerErrorHandler<T> : ConsumerErrorHandler<T>;