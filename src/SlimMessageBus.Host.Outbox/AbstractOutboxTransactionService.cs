namespace SlimMessageBus.Host.Outbox;

/// <summary>
/// Abstract base class that implements the nested-transaction state machine shared by all outbox
/// transaction-service implementations (MongoDB, PostgreSQL, SQL …).
/// Subclasses provide the DB-specific <see cref="OnBeginTransaction"/> and
/// <see cref="OnCompleteTransaction"/> hooks.
/// </summary>
public abstract class AbstractOutboxTransactionService : IAsyncDisposable
{
    private int _transactionCount;
    private bool _transactionFailed;
    private bool _transactionCompleted;

    #region IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        GC.SuppressFinalize(this);
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (!_transactionCompleted && _transactionCount > 0)
        {
            await RollbackTransaction();
        }
    }

    #endregion

    public virtual async Task BeginTransaction()
    {
        if (_transactionCompleted)
        {
            throw new MessageBusException("Transaction was completed already");
        }

        if (_transactionCount == 0)
        {
            await OnBeginTransaction();
            _transactionFailed = false;
            _transactionCompleted = false;
        }
        _transactionCount++;
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
            _transactionFailed = true;
        }

        if (!_transactionCompleted && (_transactionCount == 0 || transactionFailed))
        {
            _transactionCompleted = true;
            _transactionCount = 0;
            await OnCompleteTransaction(_transactionFailed);
        }
    }

    protected abstract Task OnBeginTransaction();

    protected abstract Task OnCompleteTransaction(bool transactionFailed);

    public virtual Task CommitTransaction() => TryCompleteTransaction(transactionFailed: false);

    public virtual Task RollbackTransaction() => TryCompleteTransaction(transactionFailed: true);
}
