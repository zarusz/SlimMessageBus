namespace SlimMessageBus.Host.Memory;

internal class NullMessageSerializer : IMessageSerializer
{
    public object Deserialize(Type t, byte[] payload, IMessageContext context) => null;
    public byte[] Serialize(Type t, object message, IMessageContext context) => null;
}
