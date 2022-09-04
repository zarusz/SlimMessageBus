namespace SlimMessageBus.Host.Config;

public class MessageTypeConsumerInvokerSettings : IMessageTypeConsumerInvokerSettings
{
    /// <summary>
    /// Represents the type of the message that is expected on the topic.
    /// </summary>
    public Type MessageType { get; }
    /// <summary>
    /// The consumer type that will handle the messages. An implementation of <see cref="IConsumer{TMessage}"/> or <see cref="IRequestHandler{TRequest,TResponse}"/>.
    /// </summary>
    public Type ConsumerType { get; }
    /// <summary>
    /// The delegate to the consumer method responsible for accepting messages.
    /// </summary>
    public Func<object, object, string, Task> ConsumerMethod { get; set; }

    public ConsumerSettings ParentSettings { get; }

    public MessageTypeConsumerInvokerSettings(ConsumerSettings parentSettings, Type messageType, Type consumerType)
    {
        MessageType = messageType;
        ConsumerType = consumerType;
        ParentSettings = parentSettings;
    }
}
