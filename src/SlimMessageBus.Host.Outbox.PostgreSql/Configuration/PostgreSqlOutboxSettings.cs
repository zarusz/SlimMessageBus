namespace SlimMessageBus.Host.Outbox.PostgreSql.Configuration;

public class PostgreSqlOutboxSettings : OutboxSettings
{
    public PostgreSqlSettings PostgreSqlSettings { get; set; } = new();
}

public class PostgreSqlSettings
{
    public string DatabaseSchemaName { get; set; } = "public";
    public string DatabaseTableName { get; set; } = "smb_outbox";
    public string DatabaseMigrationsTableName { get; set; } = "__EFMigrationsHistory";
    public TimeSpan? CommandTimeout { get; set; }
    public IsolationLevel TransactionIsolationLevel { get; set; } = IsolationLevel.ReadCommitted;

    public RetrySettings SchemaCreationRetry { get; set; } = new()
    {
        RetryCount = 3,
        RetryIntervalFactor = 1.2f,
        RetryInterval = TimeSpan.FromSeconds(2),
    };

    public RetrySettings OperationRetry { get; set; } = new()
    {
        RetryCount = 5,
        RetryIntervalFactor = 1.5f,
        RetryInterval = TimeSpan.FromSeconds(2),
    };
}

public class RetrySettings
{
    public int RetryCount { get; set; }
    public TimeSpan RetryInterval { get; set; }
    public float RetryIntervalFactor { get; set; }
}
