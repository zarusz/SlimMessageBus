namespace SlimMessageBus.Host;

public class ConsumerContext(IDictionary<string, object> properties = null) : IConsumerContext
{
    private IDictionary<string, object> _properties = properties;

    public string Path { get; set; }

    public IReadOnlyDictionary<string, object> Headers { get; set; }

    public CancellationToken CancellationToken { get; set; }

    public IMessageBus Bus { get; set; }

    public IDictionary<string, object> Properties
    {
        get
        {
            _properties ??= new Dictionary<string, object>();
            return _properties;
        }
    }

    public object Consumer { get; set; }

    public IMessageTypeConsumerInvokerSettings ConsumerInvoker { get; set; }
}
