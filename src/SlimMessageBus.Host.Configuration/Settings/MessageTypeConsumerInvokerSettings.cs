namespace SlimMessageBus.Host;

using System.Reflection;

public class MessageTypeConsumerInvokerSettings : IMessageTypeConsumerInvokerSettings
{
    /// <inheritdoc/>
    public ConsumerSettings ParentSettings { get; }
    /// <inheritdoc/>
    public Type MessageType { get; }
    /// <inheritdoc/>
    public Type ConsumerType { get; }
    /// <inheritdoc/>
    public Func<object, object, Task> ConsumerMethod { get; set; }
    /// <inheritdoc/>
    public MethodInfo ConsumerMethodInfo { get; set; }

    public MessageTypeConsumerInvokerSettings(ConsumerSettings parentSettings, Type messageType, Type consumerType)
    {
        MessageType = messageType;
        ConsumerType = consumerType;
        ParentSettings = parentSettings;
    }
}
