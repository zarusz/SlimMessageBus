namespace SlimMessageBus.Host.Sql;

public interface ISqlConsumerErrorHandler<in T> : IConsumerErrorHandler<T>;

public abstract class SqlConsumerErrorHandler<T> : ConsumerErrorHandler<T>, ISqlConsumerErrorHandler<T>;
