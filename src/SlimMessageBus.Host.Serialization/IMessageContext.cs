namespace SlimMessageBus.Host.Serialization;

/// <summary>
/// Message context for serializing and deserializing a message.
/// </summary>
public interface IMessageContext
{
    /// <summary>
    /// Gets the path for the current message.
    /// </summary>
    string Path { get; }
}