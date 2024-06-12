namespace SlimMessageBus.Host.Outbox;

public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string BusName { get; set; }
    public Type MessageType { get; set; }
    public byte[] MessagePayload { get; set; }
    public string Path { get; set; }
    public IDictionary<string, object> Headers { get; set; }
    public string InstanceId { get; set; }
    public string LockInstanceId { get; set; } = string.Empty;
    public DateTime? LockExpiresOn { get; set; } = null;
    public int DeliveryAttempt { get; set; } = 0;
    public bool DeliveryComplete { get; set; } = false;
    public bool DeliveryAborted { get; set; } = false;
}
