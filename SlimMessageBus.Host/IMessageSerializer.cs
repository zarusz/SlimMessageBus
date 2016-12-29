using System;

namespace SlimMessageBus.Host
{
    public interface IMessageSerializer
    {
        byte[] Serialize(Type t, object message);
        object Deserialize(Type t, byte[] payload);
    }
}