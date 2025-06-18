namespace SlimMessageBus.Host;

/// <summary>
/// Base builder for request builders (those who have request/response, and those who have no response).
/// </summary>
/// <typeparam name="TRequest"></typeparam>
/// <typeparam name="THandlerBuilder"></typeparam>
public abstract class AbstractHandlerBuilder<TRequest, THandlerBuilder> : AbstractConsumerBuilder<THandlerBuilder>
    where THandlerBuilder : AbstractHandlerBuilder<TRequest, THandlerBuilder>
{
    protected AbstractHandlerBuilder(MessageBusSettings settings, Type messageType, string path = null)
        : base(settings, messageType, path)
    {
    }

    protected THandlerBuilder TypedThis => (THandlerBuilder)this;

    /// <summary>
    /// Configure topic name (or queue name) that incoming requests (<see cref="TRequest"/>) are expected on.
    /// </summary>
    /// <param name="path">Topic name</param>
    /// <returns></returns>
    public THandlerBuilder Path(string path)
    {
        var consumerSettingsExist = Settings.Consumers.Any(x => x.Path == path && x.ConsumerMode == ConsumerMode.RequestResponse && x != ConsumerSettings);
        if (consumerSettingsExist)
        {
            throw new ConfigurationMessageBusException($"Attempted to configure request handler for path '{path}' when one was already configured. There can only be one request handler for a given path.");
        }

        ConsumerSettings.Path = path;
        return TypedThis;
    }

    /// <summary>
    /// Configure topic name (or queue name) that incoming requests (<see cref="TRequest"/>) are expected on.
    /// </summary>
    /// <param name="path">Topic name</param>
    /// <param name="pathConfig"></param>
    /// <returns></returns>
    public THandlerBuilder Path(string path, Action<THandlerBuilder> pathConfig)
    {
        Path(path);
        pathConfig?.Invoke(TypedThis);
        return TypedThis;
    }

    /// <summary>
    /// Configure topic name (or queue name) that incoming requests (<see cref="TRequest"/>) are expected on.
    /// </summary>
    /// <param name="topic">Topic name</param>
    /// <returns></returns>
    public THandlerBuilder Topic(string topic)
        => Path(topic);

    /// <summary>
    /// Configure topic name (or queue name) that incoming requests (<see cref="TRequest"/>) are expected on.
    /// </summary>
    /// <param name="topic">Topic name</param>
    /// <param name="topicConfig"></param>
    /// <returns></returns>
    public THandlerBuilder Topic(string topic, Action<THandlerBuilder> topicConfig)
        => Path(topic, topicConfig);

    public THandlerBuilder Instances(int numberOfInstances)
    {
        ConsumerSettings.Instances = numberOfInstances;
        return TypedThis;
    }

    public THandlerBuilder WithHandler(Type handlerType)
    {
        ConsumerSettings.ConsumerType = handlerType;
        SetupConsumerOnHandleMethod(ConsumerSettings);

        ConsumerSettings.Invokers.Add(ConsumerSettings);

        return TypedThis;
    }

    /// <summary>
    /// Declares type the handler of a derived message.
    /// The handler type has to implement <see cref="IRequestHandler{TRequest, TResponse}"/> interface.
    /// </summary>
    /// <param name="derivedRequestType">The derived request type from type <see cref="TRequest"/>/param>
    /// <param name="derivedHandlerType">The derived request handler</param>
    /// <returns></returns>
    public THandlerBuilder WithHandler(Type derivedHandlerType, Type derivedRequestType)
    {
        AssertInvokerUnique(derivedHandlerType, derivedRequestType);

        if (!ConsumerSettings.MessageType.IsAssignableFrom(derivedRequestType))
        {
            throw new ConfigurationMessageBusException($"The (derived) message type {derivedRequestType} is not assignable to message type {ConsumerSettings.MessageType}");
        }

        var invoker = new MessageTypeConsumerInvokerSettings(ConsumerSettings, messageType: derivedRequestType, consumerType: derivedHandlerType);
        SetupConsumerOnHandleMethod(invoker);
        ConsumerSettings.Invokers.Add(invoker);

        return TypedThis;
    }
}
