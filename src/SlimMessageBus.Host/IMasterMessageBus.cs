namespace SlimMessageBus.Host;

public interface IMasterMessageBus : IMessageBusProducer, IConsumerControl, ITopologyControl, IMessageBusProvider
{
    IMessageSerializerProvider SerializerProvider { get; }
}
