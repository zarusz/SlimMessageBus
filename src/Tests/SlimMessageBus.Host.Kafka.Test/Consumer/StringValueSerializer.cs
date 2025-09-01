namespace SlimMessageBus.Host.Kafka.Test;

using System.Text;

using SlimMessageBus.Host.Serialization;

/// <summary>
/// Serializes any value passed into into UTF-8 string. Prior serialization the value is converted to string using <see cref="object.ToString"/>.
/// </summary>
public class StringValueSerializer : IMessageSerializer
{
    public byte[] Serialize(Type messageType, IDictionary<string, object> headers, object message, object transportMessage)
        => Encoding.UTF8.GetBytes((string)message);

    public object Deserialize(Type messageType, IReadOnlyDictionary<string, object> headers, byte[] payload, object transportMessage)
        => Encoding.UTF8.GetString(payload);
}
