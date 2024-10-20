namespace SlimMessageBus.Host;

public abstract class AbstractConsumerSettings : HasProviderExtensions
{
    /// <summary>
    /// The settings for the message bus to which the consumer belongs.
    /// </summary>
    public MessageBusSettings MessageBusSettings { get; set; }

    /// <summary>
    /// The topic or queue name.
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// Determines the kind of the path 
    /// </summary>
    public PathKind PathKind { get; set; } = PathKind.Topic;

    /// <summary>
    /// Number of concurrent competing consumer instances to be created for the bus.
    /// This dictates how many concurrent messages can be processed at a time.
    /// </summary>
    public int Instances { get; set; }

    /// <summary>
    /// <see cref="IConsumerCircuitBreaker"/> to be used with the consumer.
    /// </summary>
    public TypeCollection<IConsumerCircuitBreaker> CircuitBreakers { get; } = [];

    protected AbstractConsumerSettings()
    {
        Instances = 1;
    }
}