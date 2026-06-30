namespace SlimMessageBus.Host.PostgreSql;

public class PostgreSqlMessageBusSettings
{
    public string? ConnectionString { get; set; }
    public string DatabaseSchemaName { get; set; } = "public";
    public string DatabaseTableName { get; set; } = "messages";
    public string DatabaseMigrationsTableName { get; set; } = "__EFMigrationsHistory";
    public TimeSpan? CommandTimeout { get; set; }
    public IsolationLevel TransactionIsolationLevel { get; set; } = IsolationLevel.ReadCommitted;
    public TimeSpan PollDelay { get; set; } = TimeSpan.FromMilliseconds(250);
    public TimeSpan LockDuration { get; set; } = TimeSpan.FromSeconds(30);
    public int PollBatchSize { get; set; } = 10;
    public int MaxDeliveryAttempts { get; set; } = 10;
    public bool NotifyOnPublish { get; set; } = true;
    public PostgreSqlMessageIdGenerationSettings IdGeneration { get; set; } = new();

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
