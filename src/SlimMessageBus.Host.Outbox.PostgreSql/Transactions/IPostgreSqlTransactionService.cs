namespace SlimMessageBus.Host.Outbox.PostgreSql.Transactions;

public interface IPostgreSqlTransactionService : IAsyncDisposable
{
    NpgsqlTransaction? CurrentTransaction { get; }
    Task BeginTransaction();
    Task CommitTransaction();
    Task RollbackTransaction();
}