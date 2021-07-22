namespace SlimMessageBus.Host.Kafka.Test
{
    using System;
    using SlimMessageBus.Host.Serialization;
    using System.Text;

    /// <summary>
    /// Serializes any value passed into into UTF-8 string. Prior serialization the value is converted to string using <see cref="object.ToString"/>.
    /// </summary>
    public class StringValueSerializer : IMessageSerializer
    {
        public object Deserialize(Type t, byte[] payload)
            => Encoding.UTF8.GetString(payload);

        public byte[] Serialize(Type t, object message)
            => Encoding.UTF8.GetBytes((string)message);
    }
}
