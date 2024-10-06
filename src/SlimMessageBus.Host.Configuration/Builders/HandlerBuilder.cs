namespace SlimMessageBus.Host;

/// <summary>
/// Base builder for request builders (those who have request/response, and those who have no response).
/// </summary>
/// <typeparam name="TRequest"></typeparam>
/// <typeparam name="THandlerBuilder"></typeparam>
public abstract class AbstractHandlerBuilder<TRequest, THandlerBuilder> : AbstractConsumerBuilder
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

    public THandlerBuilder Do(Action<THandlerBuilder> action) =>
        base.Do(action);

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

/// <summary>
/// Builder for Request-Response handlers <see cref="IRequestHandler{TRequest, TResponse}"/>
/// </summary>
/// <typeparam name="TRequest">The request type</typeparam>
/// <typeparam name="TResponse"><The response type/typeparam>
public class HandlerBuilder<TRequest, TResponse> : AbstractHandlerBuilder<TRequest, HandlerBuilder<TRequest, TResponse>>
{
    public HandlerBuilder(MessageBusSettings settings, Type requestType = null, Type responseType = null)
        : base(settings, requestType ?? typeof(TRequest))
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));

        ConsumerSettings.ConsumerMode = ConsumerMode.RequestResponse;
        ConsumerSettings.ResponseType = responseType ?? typeof(TResponse);

        if (ConsumerSettings.ResponseType == null)
        {
            throw new ConfigurationMessageBusException($"The {nameof(ConsumerSettings)}.{nameof(ConsumerSettings.ResponseType)} is not set");
        }
    }

    private static Task<TRes> DefaultHandlerOnMethod<TReq, TRes>(object consumer, object message, IConsumerContext consumerContext, CancellationToken cancellationToken)
        => ((IRequestHandler<TReq, TRes>)consumer).OnHandle((TReq)message, cancellationToken);

    private static Task<TRes> DefaultHandlerOnMethodOfContext<TReq, TRes>(object consumer, object message, IConsumerContext consumerContext, CancellationToken cancellationToken)
        => ((IRequestHandler<IConsumerContext<TReq>, TRes>)consumer).OnHandle(new MessageConsumerContext<TReq>(consumerContext, (TReq)message), cancellationToken);

    public HandlerBuilder<TRequest, TResponse> WithHandler<THandler>()
        where THandler : IRequestHandler<TRequest, TResponse>
    {
        ConsumerSettings.ConsumerType = typeof(THandler);
        ConsumerSettings.ConsumerMethod = DefaultHandlerOnMethod<TRequest, TResponse>;
        ConsumerSettings.Invokers.Add(ConsumerSettings);
        return this;
    }

    /// <summary>
    /// Declares type <typeparamref name="THandler"/> as the consumer of the derived message <typeparamref name="TDerivedRequest"/>.
    /// The consumer type has to implement <see cref="IRequestHandler{TDerivedRequest, TResponse}"/> interface.
    /// </summary>
    /// <typeparam name="THandler"></typeparam>
    /// <typeparam name="TDerivedRequest"></typeparam>
    /// <returns></returns>
    public HandlerBuilder<TRequest, TResponse> WithHandler<THandler, TDerivedRequest>()
        where THandler : class, IRequestHandler<TDerivedRequest, TResponse>
        where TDerivedRequest : TRequest
    {
        AssertInvokerUnique(derivedConsumerType: typeof(THandler), derivedMessageType: typeof(TDerivedRequest));

        var invoker = new MessageTypeConsumerInvokerSettings(ConsumerSettings, messageType: typeof(TDerivedRequest), consumerType: typeof(THandler))
        {
            ConsumerMethod = DefaultHandlerOnMethod<TDerivedRequest, TResponse>
        };
        ConsumerSettings.Invokers.Add(invoker);

        return this;
    }

    public HandlerBuilder<TRequest, TResponse> WithHandlerOfContext<THandler>()
        where THandler : IRequestHandler<IConsumerContext<TRequest>, TResponse>
    {
        ConsumerSettings.ConsumerType = typeof(THandler);
        ConsumerSettings.ConsumerMethod = DefaultHandlerOnMethodOfContext<TRequest, TResponse>;
        ConsumerSettings.Invokers.Add(ConsumerSettings);
        return this;
    }

    /// <summary>
    /// Declares type <typeparamref name="THandler"/> as the consumer of the derived message <typeparamref name="TDerivedRequest"/>.
    /// The consumer type has to implement <see cref="IRequestHandler{TDerivedRequest, TResponse}"/> interface.
    /// </summary>
    /// <typeparam name="THandler"></typeparam>
    /// <typeparam name="TDerivedRequest"></typeparam>
    /// <returns></returns>
    public HandlerBuilder<TRequest, TResponse> WithHandlerOfContext<THandler, TDerivedRequest>()
        where THandler : class, IRequestHandler<IConsumerContext<TDerivedRequest>, TResponse>
        where TDerivedRequest : TRequest
    {
        AssertInvokerUnique(derivedConsumerType: typeof(THandler), derivedMessageType: typeof(TDerivedRequest));

        var invoker = new MessageTypeConsumerInvokerSettings(ConsumerSettings, messageType: typeof(TDerivedRequest), consumerType: typeof(THandler))
        {
            ConsumerMethod = DefaultHandlerOnMethodOfContext<TDerivedRequest, TResponse>
        };
        ConsumerSettings.Invokers.Add(invoker);

        return this;
    }
}

/// <summary>
/// The handler builder for handlers that expect no response message.
/// </summary>
/// <typeparam name="TRequest"></typeparam>
public class HandlerBuilder<TRequest> : AbstractHandlerBuilder<TRequest, HandlerBuilder<TRequest>>
{
    public HandlerBuilder(MessageBusSettings settings, Type requestType = null)
        : base(settings, requestType ?? typeof(TRequest))
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        ConsumerSettings.ConsumerMode = ConsumerMode.RequestResponse;
        ConsumerSettings.ResponseType = null;
    }

    private static Task DefaultHandlerOnMethod<TReq>(object consumer, object message, IConsumerContext consumerContext, CancellationToken cancellationToken)
        => ((IRequestHandler<TReq>)consumer).OnHandle((TReq)message, cancellationToken);

    private static Task DefaultHandlerOnMethodOfContext<TReq>(object consumer, object message, IConsumerContext consumerContext, CancellationToken cancellationToken)
        => ((IRequestHandler<IConsumerContext<TReq>>)consumer).OnHandle(new MessageConsumerContext<TReq>(consumerContext, (TReq)message), cancellationToken);

    public HandlerBuilder<TRequest> WithHandler<THandler>()
        where THandler : IRequestHandler<TRequest>
    {
        ConsumerSettings.ConsumerType = typeof(THandler);
        ConsumerSettings.ConsumerMethod = DefaultHandlerOnMethod<TRequest>;
        ConsumerSettings.Invokers.Add(ConsumerSettings);
        return TypedThis;
    }

    /// <summary>
    /// Declares type <typeparamref name="THandler"/> as the consumer of the derived message <typeparamref name="TDerivedRequest"/>.
    /// The consumer type has to implement <see cref="IRequestHandler{TDerivedRequest, TResponse}"/> interface.
    /// </summary>
    /// <typeparam name="THandler"></typeparam>
    /// <typeparam name="TDerivedRequest"></typeparam>
    /// <returns></returns>
    public HandlerBuilder<TRequest> WithHandler<THandler, TDerivedRequest>()
        where THandler : class, IRequestHandler<TDerivedRequest>
        where TDerivedRequest : TRequest
    {
        AssertInvokerUnique(derivedConsumerType: typeof(THandler), derivedMessageType: typeof(TDerivedRequest));

        var invoker = new MessageTypeConsumerInvokerSettings(ConsumerSettings, messageType: typeof(TDerivedRequest), consumerType: typeof(THandler))
        {
            ConsumerMethod = DefaultHandlerOnMethod<TDerivedRequest>
        };
        ConsumerSettings.Invokers.Add(invoker);

        return this;
    }

    public HandlerBuilder<TRequest> WithHandlerOfContext<THandler>()
        where THandler : IRequestHandler<IConsumerContext<TRequest>>
    {
        ConsumerSettings.ConsumerType = typeof(THandler);
        ConsumerSettings.ConsumerMethod = DefaultHandlerOnMethodOfContext<TRequest>;
        ConsumerSettings.Invokers.Add(ConsumerSettings);
        return TypedThis;
    }

    /// <summary>
    /// Declares type <typeparamref name="THandler"/> as the consumer of the derived message <typeparamref name="TDerivedRequest"/>.
    /// The consumer type has to implement <see cref="IRequestHandler{IConsumerContext{TDerivedRequest}, TResponse}"/> interface.
    /// </summary>
    /// <typeparam name="THandler"></typeparam>
    /// <typeparam name="TDerivedRequest"></typeparam>
    /// <returns></returns>
    public HandlerBuilder<TRequest> WithHandlerOfContext<THandler, TDerivedRequest>()
        where THandler : class, IRequestHandler<IConsumerContext<TDerivedRequest>>
        where TDerivedRequest : TRequest
    {
        AssertInvokerUnique(derivedConsumerType: typeof(THandler), derivedMessageType: typeof(TDerivedRequest));

        var invoker = new MessageTypeConsumerInvokerSettings(ConsumerSettings, messageType: typeof(TDerivedRequest), consumerType: typeof(THandler))
        {
            ConsumerMethod = DefaultHandlerOnMethodOfContext<TDerivedRequest>
        };
        ConsumerSettings.Invokers.Add(invoker);

        return this;
    }
}
