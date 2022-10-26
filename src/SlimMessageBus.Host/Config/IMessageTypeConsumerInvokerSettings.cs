namespace SlimMessageBus.Host.Config;

public interface IMessageTypeConsumerInvokerSettings
{
    /// <summary>
    /// Represents the type of the message that is expected on the topic.
    /// </summary>
    Type MessageType { get; }
    /// <summary>
    /// The consumer type that will handle the messages. An implementation of <see cref="IConsumer{TMessage}"/> or <see cref="IRequestHandler{TRequest,TResponse}"/>.
    /// </summary>
    Type ConsumerType { get; }
    /// <summary>
    /// The delegate to the consumer method responsible for accepting messages.
    /// </summary>
    Func<object, object, Task> ConsumerMethod { get; set; }

    ConsumerSettings ParentSettings { get; }
}
