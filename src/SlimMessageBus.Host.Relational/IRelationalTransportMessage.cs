namespace SlimMessageBus.Host.Relational;

public interface IRelationalTransportMessage
{
    Guid Id { get; }
    byte[] MessagePayload { get; }
    Dictionary<string, object> Headers { get; }
}
