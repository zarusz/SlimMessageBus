namespace SlimMessageBus.Host;

public interface ITransportProducer
{
    Task ProduceToTransport(
        object message,
        Type messageType,
        string path,
        IDictionary<string, object> messageHeaders,
        IMessageBusTarget targetBus,
        CancellationToken cancellationToken);
}