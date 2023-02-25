namespace SlimMessageBus.Host;

/// <summary>
/// Proxy to the <see cref="IMessageBusBase"/> that introduces its own <see cref="IDependencyResolver"/> for dependency lookup.
/// </summary>
public class MessageBusProxy : IMessageBus, ICompositeMessageBus
{
    /// <summary>
    /// The target of this proxy (the singleton master bus).
    /// </summary>
    public IMessageBusProducer Target { get; }
    public IServiceProvider ServiceProvider { get; }

    public MessageBusProxy(IMessageBusProducer target, IServiceProvider serviceProvider)
    {
        Target = target;
        ServiceProvider = serviceProvider;
    }

    #region Implementation of IMessageBus

    #region Implementation of IPublishBus

    public Task Publish<TMessage>(TMessage message, string path = null, IDictionary<string, object> headers = null, CancellationToken cancellationToken = default)
        => Target.Publish(message, path: path, headers: headers, cancellationToken: cancellationToken, currentServiceProvider: ServiceProvider);

    #endregion

    #region Implementation of IRequestResponseBus

    public Task<TResponseMessage> Send<TResponseMessage>(IRequest<TResponseMessage> request, string path = null, IDictionary<string, object> headers = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        => Target.SendInternal<TResponseMessage>(request, timeout: timeout, path: path, headers: headers, cancellationToken, currentServiceProvider: ServiceProvider);

    public Task<TResponseMessage> Send<TResponseMessage, TRequestMessage>(TRequestMessage request, string path = null, IDictionary<string, object> headers = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        => Target.SendInternal<TResponseMessage>(request, timeout: timeout, path: path, headers: headers, cancellationToken, currentServiceProvider: ServiceProvider);

    public Task Send(IRequest request, string path = null, IDictionary<string, object> headers = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        => Target.SendInternal<Void>(request, timeout: timeout, path: path, headers: headers, cancellationToken, currentServiceProvider: ServiceProvider);

    #endregion

    #endregion

    #region ICompositeMessageBus

    public IMessageBus GetChildBus(string name)
    {
        if (Target is ICompositeMessageBus composite)
        {
            return composite.GetChildBus(name);
        }
        return null;
    }

    public IEnumerable<IMessageBus> GetChildBuses()
    {
        if (Target is ICompositeMessageBus composite)
        {
            return composite.GetChildBuses();
        }
        return Enumerable.Empty<IMessageBus>();
    }

    #endregion
}