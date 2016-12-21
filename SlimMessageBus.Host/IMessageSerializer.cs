using System;

namespace SlimMessageBus
{
    public interface IMessageSerializer
    {
        byte[] Serialize(Type t, object message);
        object Deserialize(Type t, byte[] payload);
    }
}