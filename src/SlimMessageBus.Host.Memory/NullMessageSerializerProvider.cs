namespace SlimMessageBus.Host.Memory;

internal class NullMessageSerializerProvider : IMessageSerializer, IMessageSerializerProvider
{
    public object Deserialize(Type t, byte[] payload) => null;

    public byte[] Serialize(Type t, object message) => null;

    public IMessageSerializer GetSerializer(string path) => this;
}
