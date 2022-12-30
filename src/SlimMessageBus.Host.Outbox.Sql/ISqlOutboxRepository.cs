namespace SlimMessageBus.Host.Outbox.Sql;

using Microsoft.Data.SqlClient;

public interface ISqlOutboxRepository : IOutboxRepository
{
    SqlTransaction CurrentTransaction { get; }
    ValueTask BeginTransaction();
    ValueTask CommitTransaction();
    ValueTask RollbackTransaction();
}
