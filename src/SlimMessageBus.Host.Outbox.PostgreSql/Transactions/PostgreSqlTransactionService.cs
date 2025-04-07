namespace SlimMessageBus.Host.Outbox.PostgreSql.Transactions;

public class PostgreSqlTransactionService : AbstractPostgreSqlTransactionService
{
    private readonly PostgreSqlSettings _settings;

    private NpgsqlTransaction? _transaction;

    public PostgreSqlTransactionService(NpgsqlConnection connection, PostgreSqlSettings settings)
        : base(connection)
    {
        _settings = settings;
    }

    public override NpgsqlTransaction? CurrentTransaction => _transaction;

#if NETSTANDARD2_0
    protected override Task OnBeginTransaction()
    {
        _transaction = Connection.BeginTransaction(_settings.TransactionIsolationLevel);
        return Task.CompletedTask;
    }

    protected override async Task OnCompleteTransaction(bool transactionFailed)
    {
        if (_transaction == null)
        {
            return;
        }

        if (transactionFailed)
        {
            _transaction.Rollback();
        }
        else
        {
            _transaction.Commit();
        }

        await _transaction.DisposeAsync().ConfigureAwait(false);
        _transaction = null;
    }
#else
    protected override async Task OnBeginTransaction()
    {
        _transaction = await Connection.BeginTransactionAsync(_settings.TransactionIsolationLevel).ConfigureAwait(false);
    }

    protected override async Task OnCompleteTransaction(bool transactionFailed)
    {
        if (_transaction == null)
        {
            return;
        }

        if (transactionFailed)
        {
            await _transaction.RollbackAsync().ConfigureAwait(false);
        }
        else
        {
            await _transaction.CommitAsync().ConfigureAwait(false);
        }

        await _transaction.DisposeAsync().ConfigureAwait(false);
        _transaction = null;
    }
#endif
}