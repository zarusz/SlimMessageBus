namespace SlimMessageBus.Host.Sql.Common;

public class SqlTransactionService(SqlConnection connection, ISqlSettings sqlSettings) : AbstractSqlTransactionService(connection)
{
    private SqlTransaction _transaction;

    public override SqlTransaction CurrentTransaction => _transaction;

    protected override async Task OnBeginTransation()
    {
#if NETSTANDARD2_0
        _transaction = Connection.BeginTransaction(sqlSettings.TransactionIsolationLevel);
#else
        _transaction = (SqlTransaction)await Connection.BeginTransactionAsync(sqlSettings.TransactionIsolationLevel);
#endif
    }

    protected override async Task OnCompleteTransaction(bool transactionFailed)
    {
        if (transactionFailed)
        {
#if NETSTANDARD2_0
            _transaction.Rollback();
#else
            await _transaction.RollbackAsync();
#endif
        }
        else
        {
#if NETSTANDARD2_0
            _transaction.Commit();
#else
            await _transaction.CommitAsync();
#endif
        }

#if NETSTANDARD2_0
        _transaction.Dispose();
#else
        await _transaction.DisposeAsync();
#endif

        _transaction = null;
    }
}