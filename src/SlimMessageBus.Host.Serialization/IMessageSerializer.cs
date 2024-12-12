namespace SlimMessageBus.Host.Serialization;

/// <summary>
/// Serializer for messages into byte[].
/// </summary>
public interface IMessageSerializer : IMessageSerializer<byte[]>
{
}

/// <summary>
/// Serializer for messages into the given payload type (byte[] etc).
/// </summary>
/// <typeparam name="TPayload"></typeparam>
public interface IMessageSerializer<TPayload>
{
    TPayload Serialize(Type t, object message);
    object Deserialize(Type t, TPayload payload);
}