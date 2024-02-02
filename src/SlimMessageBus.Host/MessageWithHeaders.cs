namespace SlimMessageBus.Host;

public readonly struct MessageWithHeaders
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
}
