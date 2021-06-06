namespace SlimMessageBus.Host.Serialization
{
    using System;

    public interface IMessageSerializer
    {
        byte[] Serialize(Type t, object message);
        object Deserialize(Type t, byte[] payload);
    }
}