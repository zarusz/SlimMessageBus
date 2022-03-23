namespace SlimMessageBus.Host
{
    public interface IMasterMessageBus : IMessageBus, IConsumerControl, IMessageBusProducer
    {
    }
}