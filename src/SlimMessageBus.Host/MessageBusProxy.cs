namespace SlimMessageBus.Host;

/// <summary>
/// Proxy to the <see cref="IMessageBusProducer"/> that introduces its own <see cref="IServiceProvider"/> for dependency lookup.
/// </summary>
public class MessageBusProxy(
    IMessageBusProducer target,
    IServiceProvider serviceProvider)
    : IMessageBusTarget, ICompositeMessageBus
{
    /// <summary>
    /// The target of this proxy (the singleton master bus).
    /// </summary>
    public IMessageBusProducer Target { get; } = target;

    public IServiceProvider ServiceProvider { get; } = serviceProvider;

    #region Implementation of IMessageBus

    #region Implementation of IPublishBus

    public Task Publish<TMessage>(TMessage message, string path = null, IDictionary<string, object> headers = null, CancellationToken cancellationToken = default)
        => Target.ProducePublish(message, path, headers, targetBus: this, cancellationToken);

    #endregion

    #region Implementation of IRequestResponseBus

    public Task<TResponseMessage> Send<TResponseMessage>(IRequest<TResponseMessage> request, string path = null, IDictionary<string, object> headers = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        => Target.ProduceSend<TResponseMessage>(request, path: path, headers: headers, timeout: timeout, targetBus: this, cancellationToken);

    public Task<TResponseMessage> Send<TResponseMessage, TRequestMessage>(TRequestMessage request, string path = null, IDictionary<string, object> headers = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        => Target.ProduceSend<TResponseMessage>(request, path: path, headers: headers, timeout: timeout, targetBus: this, cancellationToken);

    public Task Send(IRequest request, string path = null, IDictionary<string, object> headers = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        => Target.ProduceSend<Void>(request, path: path, headers: headers, timeout: timeout, targetBus: this, cancellationToken);

    #endregion

    #endregion

    #region ICompositeMessageBus

    public IMasterMessageBus GetChildBus(string name)
    {
        if (Target is ICompositeMessageBus composite)
        {
            return composite.GetChildBus(name);
        }
        return null;
    }

    public IEnumerable<IMasterMessageBus> GetChildBuses()
    {
        if (Target is ICompositeMessageBus composite)
        {
            return composite.GetChildBuses();
        }
        return Enumerable.Empty<IMasterMessageBus>();
    }

    #endregion
}