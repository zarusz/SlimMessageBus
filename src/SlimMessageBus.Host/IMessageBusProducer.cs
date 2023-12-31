namespace SlimMessageBus.Host;

public interface IMessageBusProducer
{
    Task ProducePublish(object message, string path = null, IDictionary<string, object> headers = null, IMessageBusTarget targetBus = null, CancellationToken cancellationToken = default);
    Task<TResponseMessage> ProduceSend<TResponseMessage>(object request, string path = null, IDictionary<string, object> headers = null, TimeSpan? timeout = null, IMessageBusTarget targetBus = null, CancellationToken cancellationToken = default);
}