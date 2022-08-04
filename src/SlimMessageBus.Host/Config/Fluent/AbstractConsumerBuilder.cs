namespace SlimMessageBus.Host.Config;

public abstract class AbstractConsumerBuilder
{
    public Type MessageType => ConsumerSettings.MessageType;

    public MessageBusSettings Settings { get; }

    public ConsumerSettings ConsumerSettings { get; }

    protected AbstractConsumerBuilder(MessageBusSettings settings, Type messageType, string path = null)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));

        ConsumerSettings = new ConsumerSettings
        {
            MessageType = messageType,
            Path = path,
        };
        Settings.Consumers.Add(ConsumerSettings);
    }

    public TBuilder AttachEvents<TBuilder>(Action<IConsumerEvents> eventsConfig)
        where TBuilder : AbstractConsumerBuilder
    {
        if (eventsConfig == null) throw new ArgumentNullException(nameof(eventsConfig));

        eventsConfig(ConsumerSettings);
        return (TBuilder)this;
    }

    public T Do<T>(Action<T> builder) where T : AbstractConsumerBuilder
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));

        builder((T)this);

        return (T)this;
    }

    internal static void SetupConsumerOnHandleMethod(IMessageTypeConsumerInvokerSettings consumerInvokerSettings, string methodName = null)
    {
        if (consumerInvokerSettings == null) throw new ArgumentNullException(nameof(consumerInvokerSettings));

        methodName ??= nameof(IConsumer<object>.OnHandle);

        /// See <see cref="IConsumer{TMessage}.OnHandle(TMessage, string)"/> and <see cref="IRequestHandler{TRequest, TResponse}.OnHandle(TRequest, string)"/> 
        var methodArgumentTypes = new[] { consumerInvokerSettings.MessageType, typeof(string) };
        // try to see if two param method exists
        var consumerOnHandleMethod = consumerInvokerSettings.ConsumerType.GetMethod(methodName, methodArgumentTypes);
        if (consumerOnHandleMethod != null)
        {
            var method = ReflectionUtils.GenerateAsyncMethodCallFunc2(consumerOnHandleMethod, consumerInvokerSettings.ConsumerType, consumerInvokerSettings.MessageType, typeof(string));
            consumerInvokerSettings.ConsumerMethod = (consumer, message, path) => method(consumer, message, path);
        }
        else
        {
            // try to see if one param method exists
            methodArgumentTypes = new[] { consumerInvokerSettings.MessageType };
            consumerOnHandleMethod = consumerInvokerSettings.ConsumerType.GetMethod(methodName, methodArgumentTypes);
            if (consumerOnHandleMethod != null)
            {
                var method = ReflectionUtils.GenerateAsyncMethodCallFunc1(consumerOnHandleMethod, consumerInvokerSettings.ConsumerType, consumerInvokerSettings.MessageType);
                consumerInvokerSettings.ConsumerMethod = (consumer, message, path) => method(consumer, message);
            }
        }

        Assert.IsNotNull(consumerOnHandleMethod,
            () => new ConfigurationMessageBusException($"Consumer type {consumerInvokerSettings.ConsumerType} validation error: the method {methodName} with parameters of type {consumerInvokerSettings.MessageType} and {typeof(string)} was not found."));

        // ensure the method returns a Task or Task<T>
        Assert.IsTrue(typeof(Task).IsAssignableFrom(consumerOnHandleMethod.ReturnType),
            () => new ConfigurationMessageBusException($"Consumer type {consumerInvokerSettings.ConsumerType} validation error: the response type of method {methodName} must return {typeof(Task)}"));
    }
}