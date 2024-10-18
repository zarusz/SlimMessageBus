namespace SlimMessageBus.Host.Outbox.Sql;

public class SqlOutboxSettings : OutboxSettings
{
    public SqlSettings SqlSettings { get; set; } = new();

    /// <summary>
    /// Control how the <see cref="OutboxMessage.Id"/> is being generated.
    /// </summary>
    public SqlOutboxMessageIdGenerationSettings IdGeneration { get; set; } = new();
}
