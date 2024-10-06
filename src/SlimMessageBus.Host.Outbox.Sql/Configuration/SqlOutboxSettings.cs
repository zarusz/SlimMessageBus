namespace SlimMessageBus.Host.Outbox.Sql;

public class SqlOutboxSettings : OutboxSettings
{
    public SqlSettings SqlSettings { get; set; } = new();

    /// <summary>
    /// Control how the <see cref="OutboxMessage.Id"/> is being generated.
    /// </summary>
    public SqlOutboxMessageIdGenerationSettings IdGeneration { get; set; } = new();

    /// <summary>
    /// When enabled the SQL operation execution time will be measured and logged.
    /// This will cause a bit of overhead, so it should be used for debugging purposes only.
    /// </summary>
    public bool MeasureSqlOperations { get; set; } = false;
}
