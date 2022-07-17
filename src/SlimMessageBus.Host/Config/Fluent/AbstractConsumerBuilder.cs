namespace SlimMessageBus.Host.Config
{
    using System;
    using System.Threading.Tasks;

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

            // ToDo: Convert into compiled expression

            /// See <see cref="IConsumer{TMessage}.OnHandle(TMessage, string)"/> and <see cref="IRequestHandler{TRequest, TResponse}.OnHandle(TRequest, string)"/> 
            var numArgs = 2;
            // try to see if two param method exists
            var consumerOnHandleMethod = consumerInvokerSettings.ConsumerType.GetMethod(methodName, new[] { consumerInvokerSettings.MessageType, typeof(string) });
            if (consumerOnHandleMethod == null)
            {
                // try to see if one param method exists
                numArgs = 1;
                consumerOnHandleMethod = consumerInvokerSettings.ConsumerType.GetMethod(methodName, new[] { consumerInvokerSettings.MessageType });

                Assert.IsNotNull(consumerOnHandleMethod,
                    () => new ConfigurationMessageBusException($"Consumer type {consumerInvokerSettings.ConsumerType} validation error: the method {methodName} with parameters of type {consumerInvokerSettings.MessageType} and {typeof(string)} was not found."));
            }

            // ensure the method returns a Task or Task<T>
            Assert.IsTrue(typeof(Task).IsAssignableFrom(consumerOnHandleMethod.ReturnType),
                () => new ConfigurationMessageBusException($"Consumer type {consumerInvokerSettings.ConsumerType} validation error: the response type of method {methodName} must return {typeof(Task)}"));

            consumerInvokerSettings.ConsumerMethod = numArgs == 2
                ? (consumer, message, path) => (Task)consumerOnHandleMethod.Invoke(consumer, new[] { message, path })
                : (consumer, message, path) => (Task)consumerOnHandleMethod.Invoke(consumer, new[] { message });
        }
    }
}