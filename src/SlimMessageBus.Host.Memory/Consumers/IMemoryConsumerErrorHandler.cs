namespace SlimMessageBus.Host.Memory;

public interface IMemoryConsumerErrorHandler<in T> : IConsumerErrorHandler<T>;

public abstract class MemoryConsumerErrorHandler<T> : ConsumerErrorHandler<T>;