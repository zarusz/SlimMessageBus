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
    /// The bus that was used to produce the message.
    /// For hybrid bus this will the child bus that was identified as the one to handle the message.
    /// </summary>
    public IMessageBus Bus { get; set; }
    /// <summary>
    /// Additional transport provider specific features or user custom data.
    /// </summary>
    IDictionary<string, object> Properties { get; }
}
