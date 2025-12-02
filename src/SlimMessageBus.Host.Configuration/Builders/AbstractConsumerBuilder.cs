namespace SlimMessageBus.Host;

public abstract class AbstractConsumerBuilder : IAbstractConsumerBuilder, IConsumerBuilder, IHasPostConfigurationActions
{
    public MessageBusSettings Settings { get; }

    public ConsumerSettings ConsumerSettings { get; }

    public IList<Action<IServiceCollection>> PostConfigurationActions { get; } = [];

    AbstractConsumerSettings IAbstractConsumerBuilder.ConsumerSettings => ConsumerSettings;

    HasProviderExtensions IBuilderWithSettings.Settings => ConsumerSettings;

    protected AbstractConsumerBuilder(MessageBusSettings settings, Type messageType, string path = null)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));

        ConsumerSettings = new ConsumerSettings
        {
            MessageBusSettings = settings,
            MessageType = messageType,
            Path = path,
        };
        Settings.Consumers.Add(ConsumerSettings);
    }

    static internal void SetupConsumerOnHandleMethod(IMessageTypeConsumerInvokerSettings invoker, string methodName = null)
    {
        static bool ParameterMatch(IMessageTypeConsumerInvokerSettings invoker, MethodInfo methodInfo)
        {
            var parameters = new List<Type>(methodInfo.GetParameters().Select(x => x.ParameterType));

            var consumerContextOfMessageType = typeof(IConsumerContext<>).MakeGenericType(invoker.MessageType);

            if (!parameters.Remove(invoker.MessageType)
                && !parameters.Remove(consumerContextOfMessageType))
            {
                return false;
            }

            var allowedParameters = new[] { typeof(IConsumerContext), typeof(CancellationToken) };
            foreach (var parameter in allowedParameters)
            {
                parameters.Remove(parameter);
            }

            if (parameters.Count != 0)
            {
                return false;
            }

            // ensure the method returns a Task or Task<T>
            if (!typeof(Task).IsAssignableFrom(methodInfo.ReturnType))
            {
                return false;
            }

            return true;
        }

#if NETSTANDARD2_0
        if (invoker == null) throw new ArgumentNullException(nameof(invoker));
#else
        ArgumentNullException.ThrowIfNull(invoker);
#endif

        methodName ??= nameof(IConsumer<object>.OnHandle);

        /// See <see cref="IConsumer{TMessage}.OnHandle(TMessage, CancellationToken)"/> and <see cref="IRequestHandler{TRequest, TResponse}.OnHandle(TRequest, CancellationToken)"/> 

        var consumerOnHandleMethod = invoker.ConsumerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(x => x.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase) && ParameterMatch(invoker, x))
            .OrderByDescending(x => x.GetParameters().Length)
            .FirstOrDefault();

        if (consumerOnHandleMethod == null)
        {
            throw new ConfigurationMessageBusException($"Consumer type {invoker.ConsumerType} validation error: no suitable method candidate with name {methodName} can be found");
        }

        invoker.ConsumerMethodInfo = consumerOnHandleMethod;
    }

    protected internal void AssertInvokerUnique(Type derivedConsumerType, Type derivedMessageType)
    {
        if (ConsumerSettings.Invokers.Any(x => x.MessageType == derivedMessageType && x.ConsumerType == derivedConsumerType))
        {
            throw new ConfigurationMessageBusException($"The (derived) message type {derivedMessageType} and consumer type {derivedConsumerType} is already declared on the consumer for message type {ConsumerSettings.MessageType}");
        }
    }
}

public abstract class AbstractConsumerBuilder<TConsumerBuilder>(MessageBusSettings settings, Type messageType, string path = null)
    : AbstractConsumerBuilder(settings, messageType, path)
    where TConsumerBuilder : AbstractConsumerBuilder<TConsumerBuilder>
{
    public TConsumerBuilder Do(Action<TConsumerBuilder> builder)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));

        builder((TConsumerBuilder)this);

        return (TConsumerBuilder)this;
    }

    /// <summary>
    /// Configures what should happen when an undeclared message type (or unhandled message type) arrives on the topic/queue.
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    public TConsumerBuilder WhenUndeclaredMessageTypeArrives(Action<UndeclaredMessageTypeSettings> action)
    {
        action(ConsumerSettings.UndeclaredMessageType);
        return (TConsumerBuilder)this;
    }

    /// <summary> 
    /// Filter arriving messages by headers. When filter returns false, this consumer will not be invoked for that arrival. 
    /// Example: .Filter(headers => headers.TryGetValue("ResourceType", out var v) && (string)v == nameof(MyMessage)) 
    /// </summary> 
    public TConsumerBuilder Filter(Func<IReadOnlyDictionary<string, object>, bool> headerPredicate)
    {
        if (headerPredicate == null) throw new ArgumentNullException(nameof(headerPredicate));
        ConsumerSettings.Filter = (headers, transportMessage) => headerPredicate(headers);
        return (TConsumerBuilder)this;
    }

    /// <summary> 
    /// More advanced overload where transport message is passed as well. 
    /// </summary> 
    public TConsumerBuilder Filter(Func<IReadOnlyDictionary<string, object>, object, bool> headerPredicateWithTransport)
    {
        if (headerPredicateWithTransport == null) throw new ArgumentNullException(nameof(headerPredicateWithTransport));
        ConsumerSettings.Filter = headerPredicateWithTransport;
        return (TConsumerBuilder)this;
    }
}