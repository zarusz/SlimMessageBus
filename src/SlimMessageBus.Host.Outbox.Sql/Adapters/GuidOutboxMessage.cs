namespace SlimMessageBus.Host.Outbox.Sql;

public class GuidOutboxMessage : OutboxMessage, IEquatable<GuidOutboxMessage>
{
    public Guid Id { get; set; }

    public override string ToString() => Id.ToString();

    public override bool Equals(object obj)
        => Equals(obj as GuidOutboxMessage);

    public bool Equals(GuidOutboxMessage other)
        => other is not null && Id.Equals(other.Id);

    public override int GetHashCode() => Id.GetHashCode();
}
