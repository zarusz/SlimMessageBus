namespace SlimMessageBus.Host;

public class ConsumerBuilder<T> : AbstractConsumerBuilder
{
    public ConsumerBuilder(MessageBusSettings settings, Type messageType = null)
        : base(settings, messageType ?? typeof(T))
    {
        ConsumerSettings.ConsumerMode = ConsumerMode.Consumer;
    }

    public ConsumerBuilder<T> Path(string path)
    {
        ConsumerSettings.Path = path;
        return this;
    }

    public ConsumerBuilder<T> Topic(string topic) => Path(topic);

    public ConsumerBuilder<T> Path(string path, Action<ConsumerBuilder<T>> pathConfig)
    {
        if (pathConfig is null) throw new ArgumentNullException(nameof(pathConfig));

        var b = Path(path);
        pathConfig(b);
        return b;
    }

    public ConsumerBuilder<T> Topic(string topic, Action<ConsumerBuilder<T>> topicConfig) => Path(topic, topicConfig);

    /// <summary>
    /// Declares type <typeparamref name="TConsumer"/> as the consumer of messages <typeparamref name="TMessage"/>.
    /// The consumer type has to implement <see cref="IConsumer{T}"/> interface.
    /// </summary>
    /// <typeparam name="TConsumer"></typeparam>
    /// <returns></returns>
    public ConsumerBuilder<T> WithConsumer<TConsumer>()
        where TConsumer : class, IConsumer<T>
    {
        ConsumerSettings.ConsumerType = typeof(TConsumer);
        ConsumerSettings.ConsumerMethod = (consumer, message) => ((IConsumer<T>)consumer).OnHandle((T)message);

        ConsumerSettings.Invokers.Add(ConsumerSettings);

        return this;
    }

    /// <summary>
    /// Declares type <typeparamref name="TConsumer"/> as the consumer of the derived message <typeparamref name="TMessage"/>.
    /// The consumer type has to implement <see cref="IConsumer{TMessage}"/> interface.
    /// </summary>
    /// <typeparam name="TConsumer"></typeparam>
    /// <returns></returns>
    public ConsumerBuilder<T> WithConsumer<TConsumer, TMessage>()
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : T
    {
        AssertInvokerUnique(derivedConsumerType: typeof(TConsumer), derivedMessageType: typeof(TMessage));

        var invoker = new MessageTypeConsumerInvokerSettings(ConsumerSettings, messageType: typeof(TMessage), consumerType: typeof(TConsumer))
        {
            ConsumerMethod = (consumer, message) => ((IConsumer<TMessage>)consumer).OnHandle((TMessage)message)
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
    public ConsumerBuilder<T> WithConsumer(Type derivedConsumerType, Type derivedMessageType)
    {
        AssertInvokerUnique(derivedConsumerType, derivedMessageType);

        if (!ConsumerSettings.MessageType.IsAssignableFrom(derivedMessageType))
        {
            throw new ConfigurationMessageBusException($"The (derived) message type {derivedMessageType} is not assignable to message type {ConsumerSettings.MessageType}");
        }

        var invoker = new MessageTypeConsumerInvokerSettings(ConsumerSettings, messageType: derivedMessageType, consumerType: derivedConsumerType);
        SetupConsumerOnHandleMethod(invoker);
        ConsumerSettings.Invokers.Add(invoker);

        return this;
    }

    /// <summary>
    /// Declares type <typeparamref name="TConsumer"/> as the consumer of messages <typeparamref name="TMessage"/>.
    /// </summary>
    /// <typeparam name="TConsumer"></typeparam>
    /// <param name="method">Specifies how to delegate messages to the consumer type.</param>
    /// <returns></returns>
    public ConsumerBuilder<T> WithConsumer<TConsumer>(Func<TConsumer, T, Task> consumerMethod)
        where TConsumer : class
    {
        if (consumerMethod == null) throw new ArgumentNullException(nameof(consumerMethod));

        ConsumerSettings.ConsumerType = typeof(TConsumer);
        ConsumerSettings.ConsumerMethod = (consumer, message) => consumerMethod((TConsumer)consumer, (T)message);

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
    public ConsumerBuilder<T> WithConsumer<TConsumer>(string consumerMethodName)
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
    public ConsumerBuilder<T> WithConsumer(Type consumerType, string consumerMethodName = null)
    {
        _ = consumerType ?? throw new ArgumentNullException(nameof(consumerType));

        consumerMethodName ??= nameof(IConsumer<object>.OnHandle);

        ConsumerSettings.ConsumerType = consumerType;
        SetupConsumerOnHandleMethod(ConsumerSettings, consumerMethodName);

        ConsumerSettings.Invokers.Add(ConsumerSettings);

        return this;
    }

    /// <summary>
    /// Number of concurrent competing consumer instances that the bus is asking for the DI plugin.
    /// This dictates how many concurrent messages can be processed at a time.
    /// </summary>
    /// <param name="numberOfInstances"></param>
    /// <returns></returns>
    public ConsumerBuilder<T> Instances(int numberOfInstances)
    {
        ConsumerSettings.Instances = numberOfInstances;
        return this;
    }

    /// <summary>
    /// Enable (or disable) creation of DI child scope for each meesage.
    /// </summary>
    /// <param name="enabled"></param>
    /// <returns></returns>
    public ConsumerBuilder<T> PerMessageScopeEnabled(bool enabled)
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
    public ConsumerBuilder<T> DisposeConsumerEnabled(bool enabled)
    {
        ConsumerSettings.IsDisposeConsumerEnabled = enabled;
        return this;
    }

    /// <summary>
    /// Configures what should happen when an undeclared message type arrives on the topic/queue.
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    public ConsumerBuilder<T> WhenUndeclaredMessageTypeArrives(Action<UndeclaredMessageTypeSettings> action)
    {
        action(ConsumerSettings.UndeclaredMessageType);
        return this;
    }

    public ConsumerBuilder<T> Do(Action<ConsumerBuilder<T>> action) => base.Do(action);
}