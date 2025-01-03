namespace SlimMessageBus.Host.Nats;

public interface INatsConsumerErrorHandler<in T> : IConsumerErrorHandler<T>;

public abstract class NatsConsumerErrorHandler<T> : ConsumerErrorHandler<T>, INatsConsumerErrorHandler<T>;