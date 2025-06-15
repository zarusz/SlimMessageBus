namespace SlimMessageBus.Host.Memory;

internal class NullMessageSerializerProvider : IMessageSerializer, IMessageSerializerProvider
{
    public byte[] Serialize(Type messageType, IDictionary<string, object> headers, object message, object transportMessage) => [];

    public object Deserialize(Type messageType, IReadOnlyDictionary<string, object> headers, byte[] payload, object transportMessage) => null;

    public IMessageSerializer GetSerializer(string path) => this;
}
