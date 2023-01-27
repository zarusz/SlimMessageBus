namespace SlimMessageBus.Host.Outbox.Sql;

using System.Data;

public class SqlOutboxSettings : OutboxSettings
{
    public string DatabaseSchemaName { get; set; } = "dbo";
    public string DatabaseTableName { get; set; } = "Outbox";
    public string DatabaseMigrationsTableName { get; set; } = "__EFMigrationsHistory";
    public SqlDialect Dialect { get; set; } = SqlDialect.SqlServer;
    /// <summary>
    /// Desired <see cref="TransactionIsolationLevel"/> of the transaction scope created by the consumers (when <see cref="BuilderExtensions.UseTransactionScope(Config.MessageBusBuilder, bool)"/> is enabled).
    /// </summary>
    public IsolationLevel TransactionIsolationLevel { get; set; } = IsolationLevel.RepeatableRead;

    public SqlRetrySettings SchemaCreationRetry { get; set; } = new()
    {
        RetryCount = 3,
        RetryIntervalFactor = 1.2f,
        RetryInterval = TimeSpan.FromSeconds(2),
    };

    public SqlRetrySettings OperationRetry { get; set; } = new()
    {
        RetryCount = 5,
        RetryIntervalFactor = 1.5f,
        RetryInterval = TimeSpan.FromSeconds(2),
    };
    /// <summary>
    /// Initializes the connection <see cref="Microsoft.Data.SqlClient.SqlCommand.CommandTimeout"/> when set to a value.
    /// </summary>
    public TimeSpan? CommandTimeout { get; set; }
}

public enum SqlDialect
{
    SqlServer = 1
    // ToDo: More to come
}