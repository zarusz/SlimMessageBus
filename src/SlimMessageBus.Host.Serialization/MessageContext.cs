namespace SlimMessageBus.Host.Serialization;

/// <summary>
/// Message context for serializing and deserializing a message.
/// </summary>
public class MessageContext : IMessageContext
{
    public MessageContext(string path)
    {
        Path = path;
    }

    /// <summary>
    /// Gets the path for the current message.
    /// </summary>
    public string Path { get; }
}