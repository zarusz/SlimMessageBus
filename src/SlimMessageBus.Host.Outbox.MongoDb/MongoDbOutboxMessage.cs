namespace SlimMessageBus.Host.Outbox.MongoDb;

public class MongoDbOutboxMessage : OutboxMessage
{
    public Guid Id { get; set; }

    public override string ToString() => Id.ToString();
}

internal class MongoDbOutboxAdminMessage : MongoDbOutboxMessage
{
    public DateTime Timestamp { get; set; }
    public string? LockInstanceId { get; set; }
    public DateTime LockExpiresOn { get; set; }
    public int DeliveryAttempt { get; set; }
    public bool DeliveryComplete { get; set; }
    public bool DeliveryAborted { get; set; }
}
