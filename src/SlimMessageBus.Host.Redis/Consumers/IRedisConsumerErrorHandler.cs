namespace SlimMessageBus.Host.Redis;

public interface IRedisConsumerErrorHandler<in T> : IConsumerErrorHandler<T>;

public abstract class RedisConsumerErrorHandler<T> : ConsumerErrorHandler<T>, IRedisConsumerErrorHandler<T>;