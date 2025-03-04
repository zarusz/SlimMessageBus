namespace SlimMessageBus.Host.Outbox.Sql;

public class SqlOutboxMessage : OutboxMessage<Guid>;

public class SqlOutboxAdminMessage : SqlOutboxMessage
{
    public DateTime Timestamp { get; set; }
    public string InstanceId { get; set; }
    public string LockInstanceId { get; set; } = string.Empty;
    public DateTime? LockExpiresOn { get; set; } = null;
    public int DeliveryAttempt { get; set; } = 0;
    public bool DeliveryComplete { get; set; } = false;
    public bool DeliveryAborted { get; set; } = false;
}