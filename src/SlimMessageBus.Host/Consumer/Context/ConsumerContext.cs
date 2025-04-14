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

    public object Consumer { get; set; }

    public IMessageTypeConsumerInvokerSettings ConsumerInvoker { get; set; }
}
