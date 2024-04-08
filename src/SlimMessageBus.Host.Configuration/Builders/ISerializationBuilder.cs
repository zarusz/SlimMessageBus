namespace SlimMessageBus.Host;

public interface ISerializationBuilder
{
    void RegisterSerializer<TMessageSerializer>(Action<IServiceCollection> services) where TMessageSerializer : class, IMessageSerializer;
}
