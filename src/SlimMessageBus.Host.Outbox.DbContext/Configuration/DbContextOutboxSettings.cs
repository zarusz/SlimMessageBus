namespace SlimMessageBus.Host.Outbox.DbContext;

public class DbContextOutboxSettings : OutboxSettings
{
    public string DatabaseSchemaName { get; set; } = "dbo";
    public string DatabaseTableName { get; set; } = "Outbox";
}
