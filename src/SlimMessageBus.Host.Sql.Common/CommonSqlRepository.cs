namespace SlimMessageBus.Host.Sql.Common;

public abstract class CommonSqlRepository : IAsyncDisposable
{
    private readonly ICommonSqlSettings _settings;
    private SqlTransaction _transaction;

    protected ILogger Logger { get; }
    protected SqlConnection Connection { get; }

    public virtual SqlTransaction CurrentTransaction => _transaction;

    protected CommonSqlRepository(ILogger logger, ICommonSqlSettings settings, SqlConnection connection)
    {
        _settings = settings;
        Logger = logger;
        Connection = connection;
    }

    #region IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();

        GC.SuppressFinalize(this);
    }

    protected async virtual ValueTask DisposeAsyncCore()
    {
        if (_transaction != null)
        {
            await RollbackTransaction();
        }
    }

    #endregion

    public async Task EnsureConnection()
    {
        if (Connection.State != ConnectionState.Open)
        {
            await Connection.OpenAsync();
        }
    }

    protected virtual SqlCommand CreateCommand()
    {
        var cmd = Connection.CreateCommand();
        cmd.Transaction = CurrentTransaction;

        if (_settings.CommandTimeout != null)
        {
            cmd.CommandTimeout = (int)_settings.CommandTimeout.Value.TotalSeconds;
        }

        return cmd;
    }

    public string GetTableName(string tableName) => $"[{_settings.DatabaseSchemaName}].[{tableName}]";

    public Task<int> ExecuteNonQuery(SqlRetrySettings retrySettings, string sql, Action<SqlCommand> setParameters = null, CancellationToken token = default) =>
        SqlHelper.RetryIfTransientError(Logger, retrySettings, async () =>
        {
            using var cmd = CreateCommand();
            cmd.CommandText = sql;
            setParameters?.Invoke(cmd);
            return await cmd.ExecuteNonQueryAsync();
        }, token);

    public async virtual ValueTask BeginTransaction()
    {
        ValidateNoTransactionStarted();
#if NETSTANDARD2_0
        _transaction = Connection.BeginTransaction(_settings.TransactionIsolationLevel);
#else
        _transaction = (SqlTransaction)await Connection.BeginTransactionAsync(_settings.TransactionIsolationLevel);
#endif
    }

    public async virtual ValueTask CommitTransaction()
    {
        ValidateTransactionStarted();

#if NETSTANDARD2_0
        _transaction.Commit();
        _transaction.Dispose();
#else
        await _transaction.CommitAsync();
        await _transaction.DisposeAsync();
#endif

        _transaction = null;
    }

    public async virtual ValueTask RollbackTransaction()
    {
        ValidateTransactionStarted();

#if NETSTANDARD2_0
        _transaction.Rollback();
        _transaction.Dispose();
#else
        await _transaction.RollbackAsync();
        await _transaction.DisposeAsync();
#endif

        _transaction = null;
    }

    protected void ValidateNoTransactionStarted()
    {
        if (CurrentTransaction != null)
        {
            throw new MessageBusException("Transaction is already in progress");
        }
    }

    protected void ValidateTransactionStarted()
    {
        if (CurrentTransaction == null)
        {
            throw new MessageBusException("Transaction has not been started");
        }
    }
}
