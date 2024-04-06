namespace SlimMessageBus.Host.Builders
{
    using System;

    public interface IHybridSerializationBuilder
    {
        IHybridSerializerBuilderOptions RegisterSerializer<TMessageSerializer>(Action<IServiceCollection> services = null) where TMessageSerializer : class, IMessageSerializer;
    }

    public interface IHybridSerializerBuilderOptions
    {
        IHybridSerializationBuilder For(params Type[] types);
        IHybridSerializationBuilder AsDefault();
    }
}
