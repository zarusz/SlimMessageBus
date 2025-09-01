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
    /// <summary>
    /// Used to serialize an application message to the transport message payload.
    /// </summary>
    /// <param name="messageType">The produced message type</param>
    /// <param name="headers">Message headers that are set, which will be placed into the transport message (if transport supports headers). Can be updated if the serializer needs to pass additional details message headers to the deserializer.</param>
    /// <param name="message">The produced application message</param>
    /// <param name="transportMessage">The underlying transport message that will be used to transport the message. For some transports it might be null when it cannot be provided before the payload is provided. Can be used to set additional transport message properties (if that transport passes the native message before payload).</param>
    /// <returns></returns>
    TPayload Serialize(Type messageType, IDictionary<string, object> headers, object message, object transportMessage);

    /// <summary>
    /// Used to deserialize an application message from the transport message payload.
    /// </summary>
    /// <param name="messageType">The expected consumer message type</param>
    /// <param name="headers">Message headers that have been read from the transport message (if transport supports headers).</param>
    /// <param name="payload">The transport message payload to be deserialized</param>
    /// <param name="transportMessage">The underlying transport message, that is being deserialized. Can be used to understand message formatting, type or compression.</param>
    /// <returns></returns>
    object Deserialize(Type messageType, IReadOnlyDictionary<string, object> headers, TPayload payload, object transportMessage);
}


