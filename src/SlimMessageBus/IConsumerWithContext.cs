namespace SlimMessageBus;

/// <summary>
/// An extension point for <see cref="IConsumer{TMessage}"/> to recieve provider specific (for current message subject to processing).
/// </summary>
public interface IConsumerWithContext
{
    /// <summary>
    /// Current message consumer context (injected by SMB prior message OnHandle).
    /// </summary>
    IConsumerContext Context { get; set; }
}
