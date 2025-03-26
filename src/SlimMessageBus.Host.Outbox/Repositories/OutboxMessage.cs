namespace SlimMessageBus.Host.Outbox;

public class OutboxMessage
{
    public string BusName { get; set; }
    public string MessageType { get; set; }
    public byte[] MessagePayload { get; set; }
    public string Path { get; set; }
    public IDictionary<string, object> Headers { get; set; }
}
