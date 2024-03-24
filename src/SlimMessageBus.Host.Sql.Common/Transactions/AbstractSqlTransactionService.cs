namespace SlimMessageBus.Host.Sql.Common;

public abstract class AbstractSqlTransactionService(SqlConnection connection) : ISqlTransactionService, ISqlConnectionProvider
{
    private int _transactionCount;
    private bool _transactionFailed;
    private bool _transactionCompleted;

    public SqlConnection Connection { get; } = connection;

    public abstract SqlTransaction CurrentTransaction { get; }

    #region IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();

        GC.SuppressFinalize(this);
    }

    protected async virtual ValueTask DisposeAsyncCore()
    {
        if (!_transactionCompleted && _transactionCount > 0)
        {
            await RollbackTransaction();
        }
    }

    #endregion

    public async virtual Task BeginTransaction()
    {
        if (_transactionCount++ == 0)
        {
            // Start transaction
            _transactionFailed = false;
            _transactionCompleted = false;
            await OnBeginTransation();
        }
    }

    private async Task TryCompleteTransaction(bool transactionFailed = false)
    {
        if (_transactionCount == 0)
        {
            throw new MessageBusException("Transaction has not been started");
        }

        _transactionCount--;

        if (transactionFailed)
        {
            // Mark the transaction as failed
            _transactionFailed = true;
        }

        if (!_transactionCompleted && (_transactionCount == 0 || transactionFailed))
        {
            _transactionCompleted = true;
            await OnCompleteTransaction(_transactionFailed);
        }
    }

    protected abstract Task OnBeginTransation();

    protected abstract Task OnCompleteTransaction(bool transactionFailed);

    public virtual Task CommitTransaction() => TryCompleteTransaction(transactionFailed: false);

    public virtual Task RollbackTransaction() => TryCompleteTransaction(transactionFailed: true);
}
