namespace SlimMessageBus.Host.Sql.Common;

public interface ISqlTransactionService : IAsyncDisposable
{
    SqlTransaction CurrentTransaction { get; }
    Task BeginTransaction();
    Task CommitTransaction();
    Task RollbackTransaction();
}