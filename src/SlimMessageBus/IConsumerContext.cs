namespace SlimMessageBus;

public interface IConsumerContext
{
    /// <summary>
    /// Arriving message headers.
    /// </summary>
    IReadOnlyDictionary<string, object> Headers { get; }
    /// <summary>
    /// Additional transport provider specific features.
    /// </summary>
    IReadOnlyDictionary<string, object> Properties { get; }
}
