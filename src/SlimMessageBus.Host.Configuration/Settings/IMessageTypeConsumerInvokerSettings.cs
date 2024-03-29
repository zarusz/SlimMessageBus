namespace SlimMessageBus.Host;

public interface IMessageTypeConsumerInvokerSettings
{
    ConsumerSettings ParentSettings { get; }
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
    Func<object, object, IConsumerContext, CancellationToken, Task> ConsumerMethod { get; set; }
    /// <summary>
    /// The consumer method.
    /// </summary>
    MethodInfo ConsumerMethodInfo { get; set; }
}
