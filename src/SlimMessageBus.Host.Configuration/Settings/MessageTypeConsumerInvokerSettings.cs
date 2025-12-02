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
    public ConsumerMethod ConsumerMethod { get; set; }
    /// <inheritdoc/>
    public MethodInfo ConsumerMethodInfo { get; set; }
    /// <inheritdoc/> 
    public Func<IReadOnlyDictionary<string, object>, object, bool> Filter { get; set; }

    public MessageTypeConsumerInvokerSettings(ConsumerSettings parentSettings, Type messageType, Type consumerType)
    {
        MessageType = messageType;
        ConsumerType = consumerType;
        ParentSettings = parentSettings;
    }
}
