namespace SlimMessageBus.Host.Outbox.Sql;

public class SqlOutboxSettings : OutboxSettings
{
    public SqlSettings SqlSettings { get; set; } = new();
}
