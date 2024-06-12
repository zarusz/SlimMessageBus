namespace SlimMessageBus.Host.Sql.Common;

public abstract class CommonSqlRepository : ISqlConnectionProvider
{
    private readonly ISqlSettings _settings;

    protected ILogger Logger { get; }
    protected ISqlTransactionService TransactionService { get; }

    public SqlConnection Connection { get; }

    protected CommonSqlRepository(ILogger logger, ISqlSettings settings, SqlConnection connection, ISqlTransactionService transactionService)
    {
        _settings = settings;
        Logger = logger;
        Connection = connection;
        TransactionService = transactionService;
    }

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
        cmd.Transaction = TransactionService.CurrentTransaction;

        if (_settings.CommandTimeout != null)
        {
            cmd.CommandTimeout = (int)_settings.CommandTimeout.Value.TotalSeconds;
        }

        return cmd;
    }

    public string GetQualifiedName(string tableName) => $"[{_settings.DatabaseSchemaName}].[{tableName}]";

    public Task<int> ExecuteNonQuery(SqlRetrySettings retrySettings, string sql, Action<SqlCommand> setParameters = null, CancellationToken token = default) =>
        SqlHelper.RetryIfTransientError(Logger, retrySettings, async () =>
        {
            using var cmd = CreateCommand();
            cmd.CommandText = sql;
            setParameters?.Invoke(cmd);
            return await cmd.ExecuteNonQueryAsync();
        }, token);
}
