namespace SlimMessageBus.Host;

using SlimMessageBus.Host.Config;

public class ProducerContext : IProducerContext
{
    private IDictionary<string, object> _properties;

    public string Path { get; set; }

    public IDictionary<string, object> Headers { get; set; }

    public CancellationToken CancellationToken { get; set; }

    public IMessageBus Bus { get; set; }

    public ProducerSettings ProducerSettings { get; set; }

    public IDictionary<string, object> Properties
    {
        get
        {
            // Lazy load it until really needed.
            if (_properties == null)
            {
                _properties = new Dictionary<string, object>();
            }
            return _properties;
        }
    }
}
