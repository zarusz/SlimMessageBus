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
    ConsumerMethod ConsumerMethod { get; set; }
    /// <summary>
    /// The consumer method.
    /// </summary>
    MethodInfo ConsumerMethodInfo { get; set; }

    /// <summary> 
    /// Optional predicate to filter arriving messages by headers/transport message. 
    /// When set, the invoker is only considered if the predicate returns true. 
    /// Parameters: 
    ///   IReadOnlyDictionary&lt;string, object&gt; headers - arriving message headers 
    ///   object transportMessage - underlying transport message (can be null) 
    /// </summary> 
    Func<IReadOnlyDictionary<string, object>, object, bool> Filter { get; set; }
}
