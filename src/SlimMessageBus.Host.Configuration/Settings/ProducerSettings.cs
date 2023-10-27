namespace SlimMessageBus.Host;

public class ProducerSettings : HasProviderExtensions
{
    /// <summary>
    /// Message type that will be published.
    /// </summary>
    public Type MessageType { get; set; }
    /// <summary>
    /// Default topic/queue name to use when not specified during publish/send operation.
    /// </summary>
    public string DefaultPath { get; set; }
    /// <summary>
    /// Determines the kind of the path 
    /// </summary>
    public PathKind PathKind { get; set; } = PathKind.Topic;
    /// <summary>
    /// Timeout after which this message should be considered as expired by the consumer.
    /// </summary>
    public TimeSpan? Timeout { get; set; }
    /// <summary>
    /// Hook called whenver message is being produced. Can be used to add (or mutate) message headers.
    /// </summary>
    public MessageHeaderModifier<object> HeaderModifier { get; set; }
}
