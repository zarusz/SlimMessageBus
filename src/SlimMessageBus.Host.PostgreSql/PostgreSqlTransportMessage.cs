namespace SlimMessageBus.Host.PostgreSql;

public class PostgreSqlTransportMessage
{
    public Guid Id { get; set; }
    public string MessageType { get; set; } = null!;
    public byte[] MessagePayload { get; set; } = null!;
    public Dictionary<string, object>? Headers { get; set; }
    public string Path { get; set; } = null!;
    public PathKind PathKind { get; set; }
    public string? SubscriptionName { get; set; }
}
