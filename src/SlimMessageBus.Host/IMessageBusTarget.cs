namespace SlimMessageBus.Host;

public interface IMessageBusTarget : IMessageBus
{
    IServiceProvider ServiceProvider { get; }
    IMessageBusProducer Target { get; }
}
