namespace SlimMessageBus.Host.Memory;

internal class NullMessageSerializer : IMessageSerializer
{
    public object Deserialize(Type t, byte[] payload) => null;
    public byte[] Serialize(Type t, object message) => null;
}
