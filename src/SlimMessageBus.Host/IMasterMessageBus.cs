namespace SlimMessageBus.Host;

public interface IMasterMessageBus : IMessageBusProducer, IConsumerControl, ITopologyControl, IMessageBusProvider
{
    IMessageSerializer Serializer { get; }
}
