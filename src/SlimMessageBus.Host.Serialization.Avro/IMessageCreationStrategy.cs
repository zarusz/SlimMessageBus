namespace SlimMessageBus.Host.Serialization.Avro;

public interface IMessageCreationStrategy
{
    object Create(Type type);
}
