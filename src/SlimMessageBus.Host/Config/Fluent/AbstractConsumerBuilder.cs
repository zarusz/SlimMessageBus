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

    internal static void SetupConsumerOnHandleMethod(IMessageTypeConsumerInvokerSettings invoker, string methodName = null)
    {
        if (invoker == null) throw new ArgumentNullException(nameof(invoker));

        methodName ??= nameof(IConsumer<object>.OnHandle);

        /// See <see cref="IConsumer{TMessage}.OnHandle(TMessage)"/> and <see cref="IRequestHandler{TRequest, TResponse}.OnHandle(TRequest)"/> 

        var methodArgumentTypes = new[] { invoker.MessageType };
        var consumerOnHandleMethod = invoker.ConsumerType.GetMethod(methodName, methodArgumentTypes);
        if (consumerOnHandleMethod != null)
        {
            var method = ReflectionUtils.GenerateAsyncMethodCallFunc1(consumerOnHandleMethod, invoker.ConsumerType, invoker.MessageType);
            invoker.ConsumerMethod = (consumer, message) => method(consumer, message);
        }

        Assert.IsNotNull(consumerOnHandleMethod,
            () => new ConfigurationMessageBusException($"Consumer type {invoker.ConsumerType} validation error: the method {methodName} with parameters of type {invoker.MessageType} was not found."));

        // ensure the method returns a Task or Task<T>
        Assert.IsTrue(typeof(Task).IsAssignableFrom(consumerOnHandleMethod.ReturnType),
            () => new ConfigurationMessageBusException($"Consumer type {invoker.ConsumerType} validation error: the response type of method {methodName} must return {typeof(Task)}"));
    }

    protected void AssertInvokerUnique(Type derivedConsumerType, Type derivedMessageType)
    {
        if (ConsumerSettings.Invokers.Any(x => x.MessageType == derivedMessageType && x.ConsumerType == derivedConsumerType))
        {
            throw new ConfigurationMessageBusException($"The (derived) message type {derivedMessageType} and consumer type {derivedConsumerType} is already declared on the consumer for message type {ConsumerSettings.MessageType}");
        }
    }
}