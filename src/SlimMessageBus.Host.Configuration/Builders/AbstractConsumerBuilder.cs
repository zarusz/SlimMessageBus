namespace SlimMessageBus.Host;

public abstract class AbstractConsumerBuilder : IAbstractConsumerBuilder
{
    public MessageBusSettings Settings { get; }

    public ConsumerSettings ConsumerSettings { get; }

    AbstractConsumerSettings IAbstractConsumerBuilder.ConsumerSettings => ConsumerSettings;

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

        var consumerOnHandleMethod = invoker.ConsumerType.GetMethod(methodName, new[] { invoker.MessageType });
        if (consumerOnHandleMethod == null)
        {
            throw new ConfigurationMessageBusException($"Consumer type {invoker.ConsumerType} validation error: the method {methodName} with parameters of type {invoker.MessageType} was not found.");
        }

        // ensure the method returns a Task or Task<T>
        if (!typeof(Task).IsAssignableFrom(consumerOnHandleMethod.ReturnType))
        {
            throw new ConfigurationMessageBusException($"Consumer type {invoker.ConsumerType} validation error: the response type of method {methodName} must return {typeof(Task)}");
        }

        invoker.ConsumerMethodInfo = consumerOnHandleMethod;
    }

    protected void AssertInvokerUnique(Type derivedConsumerType, Type derivedMessageType)
    {
        if (ConsumerSettings.Invokers.Any(x => x.MessageType == derivedMessageType && x.ConsumerType == derivedConsumerType))
        {
            throw new ConfigurationMessageBusException($"The (derived) message type {derivedMessageType} and consumer type {derivedConsumerType} is already declared on the consumer for message type {ConsumerSettings.MessageType}");
        }
    }
}