namespace SlimMessageBus.Host;

public interface IMasterMessageBus : IMessageBus, IMessageBusProducer, IConsumerControl, ITopologyControl
{
}