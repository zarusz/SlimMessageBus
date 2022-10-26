namespace SlimMessageBus;

public interface IProducerContext
{
    /// <summary>
    /// The path (topic or queue) the will be deliverd to.
    /// </summary>
    string Path { get; }
    /// <summary>
    /// Message headers.
    /// </summary>
    IDictionary<string, object> Headers { get; }
    /// <summary>
    /// The cancellation token.
    /// </summary>
    CancellationToken CancellationToken { get; }
    /// <summary>
    /// The bus on which the producer was executed.
    /// </summary>
    IMessageBus Bus { get; }
    /// <summary>
    /// Additional transport provider specific features or user custom data.
    /// </summary>
    IDictionary<string, object> Properties { get; }
}
