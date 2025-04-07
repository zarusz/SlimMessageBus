namespace SlimMessageBus.Host.Outbox.PostgreSql;

public class PostgreSqlOutboxMessage : OutboxMessage
{
    public Guid Id { get; set; }

    public override string ToString() => Id.ToString();
}

public class PostgreSqlOutboxAdminMessage : PostgreSqlOutboxMessage
{
    public DateTimeOffset Timestamp { get; set; }
    public string? LockInstanceId { get; set; } = null;
    public DateTimeOffset? LockExpiresOn { get; set; } = null;
    public int DeliveryAttempt { get; set; } = 0;
    public bool DeliveryComplete { get; set; } = false;
    public bool DeliveryAborted { get; set; } = false;
}