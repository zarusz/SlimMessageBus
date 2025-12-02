namespace SlimMessageBus.Host;

public class ConsumerSettings : AbstractConsumerSettings, IMessageTypeConsumerInvokerSettings
{
    private Type _messageType;

    /// <inheritdoc/>
    public Type MessageType
    {
        get => _messageType;
        set
        {
            _messageType = value;
            CalculateResponseType();
        }
    }

    private void CalculateResponseType()
    {
        // Try to get T from IRequest<T>
        ResponseType = _messageType.GetInterfaces()
            .SingleOrDefault(i => i.GetTypeInfo().IsGenericType && i.GetTypeInfo().GetGenericTypeDefinition() == typeof(IRequest<>))?.GetGenericArguments()[0];
    }

    /// <summary>
    /// Type of consumer that is configured (subscriber or request handler).
    /// </summary>
    public ConsumerMode ConsumerMode { get; set; }
    /// <inheritdoc/>
    public Type ConsumerType { get; set; }
    /// <inheritdoc/>
    public ConsumerMethod ConsumerMethod { get; set; }
    /// <inheritdoc/>
    public MethodInfo ConsumerMethodInfo { get; set; }
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
    /// Determines if a child scope is created for the message consumption. The consumer instance is then derived from that scope.
    /// </summary>
    public bool? IsMessageScopeEnabled { get; set; }
    /// <summary>
    /// Enables the disposal of consumer instance after the message has been consumed.
    /// </summary>
    public bool IsDisposeConsumerEnabled { get; set; }

    /// <summary> 
    /// Optional predicate evaluated on arrival headers and transport message to decide if this invoker should be used. 
    /// </summary> 
    public ConsumerFilter<object> Filter { get; set; }
}
