namespace SlimMessageBus.Host.Config;

public class ConsumerSettings : AbstractConsumerSettings, IMessageTypeConsumerInvokerSettings
{
    private Type messageType;

    /// <inheritdoc/>
    public Type MessageType
    {
        get => messageType;
        set
        {
            messageType = value;
            CalculateResponseType();
        }
    }

    private void CalculateResponseType()
    {
        // Try to get T from IRequest<T>
        ResponseType = messageType.GetInterfaces()
            .SingleOrDefault(i => i.GetTypeInfo().IsGenericType && i.GetTypeInfo().GetGenericTypeDefinition() == typeof(IRequest<>))?.GetGenericArguments()[0];
    }

    /// Type of consumer that is configured (subscriber or request handler).
    /// </summary>
    public ConsumerMode ConsumerMode { get; set; }
    /// <inheritdoc/>
    public Type ConsumerType { get; set; }
    /// <inheritdoc/>
    public Func<object, object, Task> ConsumerMethod { get; set; }
    /// <summary>
    /// List of all declared consumers that handle any derived message type of the declared message type.
    /// </summary>
    public ISet<IMessageTypeConsumerInvokerSettings> Invokers { get; } = new HashSet<IMessageTypeConsumerInvokerSettings>();

    public ConsumerSettings ParentSettings => this;
    /// <summary>
    /// The response message that will be sent as a response to the arriving message (if request/response). Null when message type is not a request.
    /// </summary>
    public Type ResponseType { get; set; }
    /// <summary>
    /// Determines if a child scope is created for the message consuption. The consumer instance is then derived from that scope.
    /// </summary>
    public bool? IsMessageScopeEnabled { get; set; }
    /// <summary>
    /// Enables the disposal of consumer instance after the message has been consumed.
    /// </summary>
    public bool IsDisposeConsumerEnabled { get; set; }
    /// <summary>
    /// Settings that should apply when a message type arrives for which there is no declared consumers
    /// </summary>
    public UndeclaredMessageTypeSettings UndeclaredMessageType { get; set; } = new UndeclaredMessageTypeSettings();
}

public class UndeclaredMessageTypeSettings
{
    /// <summary>
    /// Should the message fail when an undeclared message type arrives on the queue/topic that cannot be handled by any of the declared consumers.
    /// </summary>
    public bool Fail { get; set; }
    /// <summary>
    /// Should the message be logged when an undeclared message type arrives on the queue/topic that cannot be handled by any of the declared consumers.
    /// </summary>
    public bool Log { get; set; }
}