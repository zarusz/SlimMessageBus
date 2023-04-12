namespace SlimMessageBus.Host;

public interface IMessageBusProducer
{
    Task ProducePublish(object message, string path = null, IDictionary<string, object> headers = null, IServiceProvider currentServiceProvider = null, CancellationToken cancellationToken = default);
    Task<TResponseMessage> ProduceSend<TResponseMessage>(object request, TimeSpan? timeout = null, string path = null, IDictionary<string, object> headers = null, IServiceProvider currentServiceProvider = null, CancellationToken cancellationToken = default);
}