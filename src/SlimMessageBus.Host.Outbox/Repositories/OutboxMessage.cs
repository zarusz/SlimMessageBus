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
    public string LockInstanceId { get; set; }
    public DateTime? LockExpiresOn { get; set; }
    public int DeliveryAttempt { get; set; }
    public bool DeliveryComplete { get; set; }
}
