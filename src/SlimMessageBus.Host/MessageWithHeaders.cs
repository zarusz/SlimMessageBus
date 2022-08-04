namespace SlimMessageBus.Host;

public struct MessageWithHeaders : IEquatable<MessageWithHeaders>
{
    // ToDo: Change to ReadOnlyMemory<byte>
    public byte[] Payload { get; }
    public IDictionary<string, object> Headers { get; }

    public MessageWithHeaders(byte[] payload)
        : this(payload, new Dictionary<string, object>())
    {
    }

    public MessageWithHeaders(byte[] payload, IDictionary<string, object> headers)
    {
        Headers = headers;
        Payload = payload;
    }

    public override bool Equals(object obj)
    {
        return obj is MessageWithHeaders headers &&
               EqualityComparer<byte[]>.Default.Equals(Payload, headers.Payload) &&
               EqualityComparer<IDictionary<string, object>>.Default.Equals(Headers, headers.Headers);
    }

    public override int GetHashCode() => HashCode.Combine(Payload, Headers);

    public static bool operator ==(MessageWithHeaders left, MessageWithHeaders right) => left.Equals(right);

    public static bool operator !=(MessageWithHeaders left, MessageWithHeaders right) => !(left == right);

    public bool Equals(MessageWithHeaders other) => 
        EqualityComparer<byte[]>.Default.Equals(Payload, other.Payload) &&
        EqualityComparer<IDictionary<string, object>>.Default.Equals(Headers, other.Headers);
}