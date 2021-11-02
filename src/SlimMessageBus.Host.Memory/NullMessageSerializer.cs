namespace SlimMessageBus.Host.Memory
{
    using System;
    using SlimMessageBus.Host.Serialization;

    internal class NullMessageSerializer : IMessageSerializer
    {
        public object Deserialize(Type t, byte[] payload) => null;
        public byte[] Serialize(Type t, object message) => null;
    }
}
