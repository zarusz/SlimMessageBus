namespace SlimMessageBus.Host;

using System.Reflection;

public abstract class AbstractConsumerBuilder : IAbstractConsumerBuilder, IHasPostConfigurationActions
{
    public MessageBusSettings Settings { get; }

    public ConsumerSettings ConsumerSettings { get; }

    public IList<Action<IServiceCollection>> PostConfigurationActions { get; } = [];

    AbstractConsumerSettings IAbstractConsumerBuilder.ConsumerSettings => ConsumerSettings;

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

    public T Do<T>(Action<T> builder) where T : AbstractConsumerBuilder
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));

        builder((T)this);

        return (T)this;
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

    protected void AssertInvokerUnique(Type derivedConsumerType, Type derivedMessageType)
    {
        if (ConsumerSettings.Invokers.Any(x => x.MessageType == derivedMessageType && x.ConsumerType == derivedConsumerType))
        {
            throw new ConfigurationMessageBusException($"The (derived) message type {derivedMessageType} and consumer type {derivedConsumerType} is already declared on the consumer for message type {ConsumerSettings.MessageType}");
        }
    }
}