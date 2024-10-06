namespace SlimMessageBus.Host.Outbox;

public class OutboxMessage<TOutboxMessageKey>
{
    public TOutboxMessageKey Id { get; set; }
    public string BusName { get; set; }
    public string MessageType { get; set; }
    public byte[] MessagePayload { get; set; }
    public string Path { get; set; }
    public IDictionary<string, object> Headers { get; set; }
    public string InstanceId { get; set; }
}
