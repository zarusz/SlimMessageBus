namespace SlimMessageBus.Host.Sql;

public class SqlTransportMessage : IRelationalTransportMessage
{
    public Guid Id { get; set; }
    public string MessageType { get; set; }
    public byte[] MessagePayload { get; set; }
    public Dictionary<string, object> Headers { get; set; }
    public string Path { get; set; }
    public PathKind PathKind { get; set; }
    public string SubscriptionName { get; set; }
}
