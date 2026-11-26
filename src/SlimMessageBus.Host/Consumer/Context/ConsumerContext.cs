namespace SlimMessageBus.Host;

public class ConsumerContext : IConsumerContext
{
    private IDictionary<string, object> _properties;

    public string Path { get; set; }

    public IReadOnlyDictionary<string, object> Headers { get; set; }

    public CancellationToken CancellationToken { get; set; } = default;

    public IMessageBus Bus { get; set; }

    public IDictionary<string, object> Properties
    {
        get => _properties ??= new Dictionary<string, object>();
        set => _properties = value;
    }

    private object _consumer;
    private Func<object> _consumerFactory;

    internal object ResolvedConsumer => _consumer;

    /// <summary>
    /// Lazily resolves the consumer instance on first access.
    /// Set by <see cref="MessageHandler"/> via <see cref="SetConsumerFactory"/> so that
    /// the consumer is created <em>after</em> all interceptors have had a chance to run
    /// (e.g. to start a database transaction before the consumer is constructed and its
    /// scoped DI dependencies are resolved).
    /// </summary>
    public object Consumer
    {
        get => _consumer ??= _consumerFactory?.Invoke();
        set => _consumer = value;
    }

    /// <summary>
    /// Sets the factory used to lazily resolve the consumer instance from DI.
    /// Called by <see cref="MessageHandler.CreateConsumerContext"/> instead of an eager
    /// <c>GetService</c> call so that consumer construction is deferred until the
    /// interceptor pipeline has completed.
    /// </summary>
    internal void SetConsumerFactory(Func<object> factory)
    {
        _consumerFactory = factory;
        _consumer = null;
    }

    public IMessageTypeConsumerInvokerSettings ConsumerInvoker { get; set; }
}
