namespace SlimMessageBus.Host.Serialization.Avro
{
    using System;

    public interface IMessageCreationStrategy
    {
        object Create(Type type);
    }
}
