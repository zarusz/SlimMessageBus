namespace SlimMessageBus.Host.Redis;

public interface IRedisConsumerErrorHandler<in T> : IConsumerErrorHandler<T>
{
}