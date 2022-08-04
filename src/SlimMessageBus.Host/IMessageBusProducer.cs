namespace SlimMessageBus.Host;

public interface IMessageBusProducer
{
    Task Publish(object message, string path = null, IDictionary<string, object> headers = null, CancellationToken cancellationToken = default, IDependencyResolver currentDependencyResolver = null);
    Task<TResponseMessage> SendInternal<TResponseMessage>(object request, TimeSpan? timeout, string path, IDictionary<string, object> headers, CancellationToken cancellationToken, IDependencyResolver currentDependencyResolver = null);
}