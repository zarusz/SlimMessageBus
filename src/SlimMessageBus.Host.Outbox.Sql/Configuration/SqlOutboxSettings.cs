namespace SlimMessageBus.Host.Outbox.Sql;

public class SqlOutboxSettings : OutboxSettings
{
    public CommonSqlSettings SqlSettings { get; set; } = new();
}
