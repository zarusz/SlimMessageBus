namespace SlimMessageBus.Host;

public struct MessageWithHeaders : IEquatable<MessageWithHeaders>
{
    // ToDo: Change to ReadOnlyMemory<byte>
    public byte[] Payload { get; }
    public IReadOnlyDictionary<string, object> Headers { get; }

    public MessageWithHeaders(byte[] payload, IReadOnlyDictionary<string, object> headers)
    {
        Payload = payload;
        Headers = headers;
    }

    public MessageWithHeaders(byte[] payload, IDictionary<string, object> headers)
    {
        Payload = payload;
        Headers = headers as IReadOnlyDictionary<string, object> ?? new Dictionary<string, object>(headers);
    }

    public MessageWithHeaders(byte[] payload, Dictionary<string, object> headers)
    {
        Payload = payload;
        Headers = headers;
    }

    public override bool Equals(object obj) =>
        obj is MessageWithHeaders headers &&
               EqualityComparer<byte[]>.Default.Equals(Payload, headers.Payload) &&
               EqualityComparer<IReadOnlyDictionary<string, object>>.Default.Equals(Headers, headers.Headers);

    public override int GetHashCode() => HashCode.Combine(Payload, Headers);

    public static bool operator ==(MessageWithHeaders left, MessageWithHeaders right) => left.Equals(right);

    public static bool operator !=(MessageWithHeaders left, MessageWithHeaders right) => !(left == right);

    public bool Equals(MessageWithHeaders other) =>
        EqualityComparer<byte[]>.Default.Equals(Payload, other.Payload) &&
        EqualityComparer<IReadOnlyDictionary<string, object>>.Default.Equals(Headers, other.Headers);
}