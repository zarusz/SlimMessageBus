namespace SlimMessageBus.Host;

public class ConsumerBuilder<TMessage> : AbstractConsumerBuilder
{
    public ConsumerBuilder(MessageBusSettings settings, Type messageType = null)
        : base(settings, messageType ?? typeof(TMessage))
    {
        ConsumerSettings.ConsumerMode = ConsumerMode.Consumer;
    }

    public ConsumerBuilder<TMessage> Path(string path, Action<ConsumerBuilder<TMessage>> pathConfig = null)
    {
        ConsumerSettings.Path = path;
        pathConfig?.Invoke(this);
        return this;
    }

    public ConsumerBuilder<TMessage> Topic(string topic, Action<ConsumerBuilder<TMessage>> topicConfig = null) => Path(topic, topicConfig);

    private static Task DefaultConsumerOnMethod<T>(object consumer, object message, IConsumerContext consumerContext, CancellationToken cancellationToken)
        => ((IConsumer<T>)consumer).OnHandle((T)message, cancellationToken);

    private static Task DefaultConsumerOnMethodOfContext<T>(object consumer, object message, IConsumerContext consumerContext, CancellationToken cancellationToken)
        => ((IConsumer<IConsumerContext<T>>)consumer).OnHandle(new MessageConsumerContext<T>(consumerContext, (T)message), cancellationToken);

    /// <summary>
    /// Declares type <typeparamref name="TConsumer"/> as the consumer of messages <typeparamref name="TMessage"/>.
    /// The consumer type has to implement <see cref="IConsumer{T}"/> interface.
    /// </summary>
    /// <typeparam name="TConsumer"></typeparam>
    /// <returns></returns>
    public ConsumerBuilder<TMessage> WithConsumer<TConsumer>()
        where TConsumer : class, IConsumer<TMessage>
    {
        ConsumerSettings.ConsumerType = typeof(TConsumer);
        ConsumerSettings.ConsumerMethod = DefaultConsumerOnMethod<TMessage>;
        ConsumerSettings.Invokers.Add(ConsumerSettings);
        return this;
    }

    /// <summary>
    /// Declares type <typeparamref name="TConsumer"/> as the consumer of the derived message <typeparamref name="TDerivedMessage"/>.
    /// The consumer type has to implement <see cref="IConsumer{TMessage}"/> interface.
    /// </summary>
    /// <typeparam name="TConsumer"></typeparam>
    /// <returns></returns>
    public ConsumerBuilder<TMessage> WithConsumer<TConsumer, TDerivedMessage>()
        where TConsumer : class, IConsumer<TDerivedMessage>
        where TDerivedMessage : TMessage
    {
        AssertInvokerUnique(derivedConsumerType: typeof(TConsumer), derivedMessageType: typeof(TDerivedMessage));

        var invoker = new MessageTypeConsumerInvokerSettings(ConsumerSettings, messageType: typeof(TDerivedMessage), consumerType: typeof(TConsumer))
        {
            ConsumerMethod = DefaultConsumerOnMethod<TDerivedMessage>
        };
        ConsumerSettings.Invokers.Add(invoker);

        return this;
    }

    /// <summary>
    /// Declares type the consumer of a derived message.
    /// The consumer type has to implement <see cref="IConsumer{TMessage}"/> interface.
    /// </summary>
    /// <typeparam name="TConsumer"></typeparam>
    /// <returns></returns>
    public ConsumerBuilder<TMessage> WithConsumer(Type derivedConsumerType, Type derivedMessageType, string methodName = null)
    {
        AssertInvokerUnique(derivedConsumerType, derivedMessageType);

        if (!ConsumerSettings.MessageType.IsAssignableFrom(derivedMessageType))
        {
            throw new ConfigurationMessageBusException($"The (derived) message type {derivedMessageType} is not assignable to message type {ConsumerSettings.MessageType}");
        }

        var invoker = new MessageTypeConsumerInvokerSettings(ConsumerSettings, messageType: derivedMessageType, consumerType: derivedConsumerType);
        SetupConsumerOnHandleMethod(invoker, methodName);
        ConsumerSettings.Invokers.Add(invoker);

        return this;
    }

    /// <summary>
    /// Declares type <typeparamref name="TConsumer"/> as the consumer of messages <typeparamref name="TMessage"/>.
    /// </summary>
    /// <typeparam name="TConsumer"></typeparam>
    /// <param name="method">Specifies how to delegate messages to the consumer type.</param>
    /// <returns></returns>
    public ConsumerBuilder<TMessage> WithConsumer<TConsumer>(Func<TConsumer, TMessage, IConsumerContext, CancellationToken, Task> consumerMethod)
        where TConsumer : class
    {
        if (consumerMethod == null) throw new ArgumentNullException(nameof(consumerMethod));

        ConsumerSettings.ConsumerType = typeof(TConsumer);
        ConsumerSettings.ConsumerMethod = (consumer, message, consumerContext, ct) => consumerMethod((TConsumer)consumer, (TMessage)message, consumerContext, ct);
        ConsumerSettings.Invokers.Add(ConsumerSettings);

        return this;
    }

    /// <summary>
    /// Declares type <typeparamref name="TConsumer"/> as the consumer of messages <typeparamref name="TMessage"/>.
    /// The consumer type has to have a method: <see cref="Task"/> <paramref name="consumerMethodName"/>(<typeparamref name="TMessage"/>, <see cref="string"/>).
    /// </summary>
    /// <typeparam name="TConsumer"></typeparam>
    /// <param name="consumerMethodName"></param>
    /// <returns></returns>
    public ConsumerBuilder<TMessage> WithConsumer<TConsumer>(string consumerMethodName)
        where TConsumer : class
    {
        if (consumerMethodName == null) throw new ArgumentNullException(nameof(consumerMethodName));

        return WithConsumer(typeof(TConsumer), consumerMethodName);
    }

    /// <summary>
    /// Declares type <typeparamref name="TConsumer"/> as the consumer of messages <typeparamref name="TMessage"/>.
    /// The consumer type has to have a method: <see cref="Task"/> <paramref name="consumerMethodName"/>(<typeparamref name="TMessage"/>, <see cref="string"/>).
    /// </summary>
    /// <param name="consumerType"></param>
    /// <param name="consumerMethodName">If null, will default to <see cref="IConsumer{TMessage}.OnHandle(TMessage, string)"/> </param>
    /// <returns></returns>
    public ConsumerBuilder<TMessage> WithConsumer(Type consumerType, string consumerMethodName = null)
    {
        _ = consumerType ?? throw new ArgumentNullException(nameof(consumerType));

        consumerMethodName ??= nameof(IConsumer<object>.OnHandle);

        ConsumerSettings.ConsumerType = consumerType;
        SetupConsumerOnHandleMethod(ConsumerSettings, consumerMethodName);

        ConsumerSettings.Invokers.Add(ConsumerSettings);

        return this;
    }

    /// <summary>
    /// Declares type <typeparamref name="TConsumer"/> as the consumer of messages <typeparamref name="TMessage"/>.
    /// The consumer type has to implement <see cref="IConsumer{IConsumerContext{TMessage}}"/> interface.
    /// </summary>
    /// <typeparam name="TConsumer"></typeparam>
    /// <returns></returns>
    public ConsumerBuilder<TMessage> WithConsumerOfContext<TConsumer>()
        where TConsumer : class, IConsumer<IConsumerContext<TMessage>>
    {
        ConsumerSettings.ConsumerType = typeof(TConsumer);
        ConsumerSettings.ConsumerMethod = DefaultConsumerOnMethodOfContext<TMessage>;
        ConsumerSettings.Invokers.Add(ConsumerSettings);
        return this;
    }

    /// <summary>
    /// Declares type <typeparamref name="TConsumer"/> as the consumer of the derived message <typeparamref name="TDerivedMessage"/>.
    /// The consumer type has to implement <see cref="IConsumer{IConsumerContext{TMessage}}"/> interface.
    /// </summary>
    /// <typeparam name="TConsumer"></typeparam>
    /// <returns></returns>
    public ConsumerBuilder<TMessage> WithConsumerOfContext<TConsumer, TDerivedMessage>()
        where TConsumer : class, IConsumer<IConsumerContext<TDerivedMessage>>
        where TDerivedMessage : TMessage
    {
        AssertInvokerUnique(derivedConsumerType: typeof(TConsumer), derivedMessageType: typeof(TDerivedMessage));

        var invoker = new MessageTypeConsumerInvokerSettings(ConsumerSettings, messageType: typeof(TDerivedMessage), consumerType: typeof(TConsumer))
        {
            ConsumerMethod = DefaultConsumerOnMethodOfContext<TDerivedMessage>
        };
        ConsumerSettings.Invokers.Add(invoker);

        return this;
    }

    /// <summary>
    /// Number of concurrent competing consumer instances that the bus is asking for the DI plugin.
    /// This dictates how many concurrent messages can be processed at a time.
    /// </summary>
    /// <param name="numberOfInstances"></param>
    /// <returns></returns>
    public ConsumerBuilder<TMessage> Instances(int numberOfInstances)
    {
        ConsumerSettings.Instances = numberOfInstances;
        return this;
    }

    /// <summary>
    /// Enable (or disable) creation of DI child scope for each meesage.
    /// </summary>
    /// <param name="enabled"></param>
    /// <returns></returns>
    public ConsumerBuilder<TMessage> PerMessageScopeEnabled(bool enabled)
    {
        ConsumerSettings.IsMessageScopeEnabled = enabled;
        return this;
    }

    /// <summary>
    /// Enable (or disable) disposal of consumer after message consumption.
    /// </summary>
    /// <remarks>This should be used in conjuction with <see cref="PerMessageScopeEnabled"/>. With per message scope enabled, the DI should dispose the consumer upon disposal of message scope.</remarks>
    /// <param name="enabled"></param>
    /// <returns></returns>
    public ConsumerBuilder<TMessage> DisposeConsumerEnabled(bool enabled)
    {
        ConsumerSettings.IsDisposeConsumerEnabled = enabled;
        return this;
    }

    /// <summary>
    /// Configures what should happen when an undeclared message type arrives on the topic/queue.
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    public ConsumerBuilder<TMessage> WhenUndeclaredMessageTypeArrives(Action<UndeclaredMessageTypeSettings> action)
    {
        action(ConsumerSettings.UndeclaredMessageType);
        return this;
    }

    public ConsumerBuilder<TMessage> Do(Action<ConsumerBuilder<TMessage>> action) => base.Do(action);
}