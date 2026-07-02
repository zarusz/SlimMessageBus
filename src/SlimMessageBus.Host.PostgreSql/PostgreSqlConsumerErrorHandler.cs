namespace SlimMessageBus.Host.PostgreSql;

public interface IPostgreSqlConsumerErrorHandler<in T> : IConsumerErrorHandler<T>;

public abstract class PostgreSqlConsumerErrorHandler<T> : ConsumerErrorHandler<T>, IPostgreSqlConsumerErrorHandler<T>;
