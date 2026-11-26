namespace SlimMessageBus;

public interface IConsumerContext
{
    /// <summary>
    /// The path (topic or queue) the message arrived on.
    /// </summary>
    string Path { get; }
    /// <summary>
    /// Arriving message headers.
    /// </summary>
    IReadOnlyDictionary<string, object> Headers { get; }
    /// <summary>
    /// The cancellation token.
    /// </summary>
    CancellationToken CancellationToken { get; }
    /// <summary>
    /// The bus on which the consumer was executed.
    /// </summary>
    IMessageBus Bus { get; }
    /// <summary>
    /// Additional transport provider specific features or user custom data.
    /// </summary>
    IDictionary<string, object> Properties { get; }
    /// <summary>
    /// The consumer instance for the current dispatch. The host sets it when it reaches the consumer invocation.
    /// The value is <see langword="null"/> until the instance is resolved.
    /// </summary>
    object Consumer { get; }
}

public interface IConsumerContext<out TMessage> : IConsumerContext
{
    public TMessage Message { get; }
}