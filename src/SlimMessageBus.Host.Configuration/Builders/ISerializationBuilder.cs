namespace SlimMessageBus.Host;

public interface ISerializationBuilder
{
    void RegisterSerializer<TMessageSerializerProvider>(Action<IServiceCollection> services)
        where TMessageSerializerProvider : class, IMessageSerializerProvider;
}
