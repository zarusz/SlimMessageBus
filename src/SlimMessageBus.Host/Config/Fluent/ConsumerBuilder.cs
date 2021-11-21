namespace SlimMessageBus.Host.Config
{
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;

    public class ConsumerBuilder<T> : AbstractConsumerBuilder
    {
        public ConsumerBuilder(MessageBusSettings settings, Type messageType = null)
            : base(settings, messageType ?? typeof(T))
        {
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
            ConsumerSettings.ConsumerMode = ConsumerMode.Consumer;
            ConsumerSettings.ConsumerType = typeof(TConsumer);
            ConsumerSettings.ConsumerMethod = (consumer, message, path) => ((IConsumer<T>)consumer).OnHandle((T)message, path);

            ConsumerSettings.ConsumersByMessageType.Add(typeof(T), ConsumerSettings);

            return this;
        }

        /// <summary>
        /// Declares type <typeparamref name="TConsumer"/> as the consumer of messages <typeparamref name="TMessage"/>.
        /// The consumer type has to implement <see cref="IConsumer{TMessage}"/> interface.
        /// </summary>
        /// <typeparam name="TConsumer"></typeparam>
        /// <returns></returns>
        public ConsumerBuilder<T> WithConsumer<TConsumer, TMessage>()
            where TConsumer : class, IConsumer<TMessage>
            where TMessage : T
        {
            if (ConsumerSettings.ConsumersByMessageType.ContainsKey(typeof(TMessage)))
            {
                throw new ConfigurationMessageBusException($"The (derived) message type {typeof(TMessage)} is already decrlared on the consumer for message type {ConsumerSettings.MessageType}");
            }

            ConsumerSettings.ConsumersByMessageType.Add(typeof(TMessage), new MessageTypeConsumerInvokerSettings
            {
                MessageType = typeof(TMessage),
                ConsumerType = typeof(TConsumer),
                ConsumerMethod = (consumer, message, path) => ((IConsumer<TMessage>)consumer).OnHandle((TMessage)message, path)
            });

            return this;
        }

        /// <summary>
        /// Finds (using reflection) types that implement IConsumer<T> in the specified assembly.
        /// The consumer type has to implement <see cref="IConsumer{TMessage}"/> interface.
        /// </summary>
        /// <typeparam name="TConsumer"></typeparam>
        /// <returns></returns>
        public ConsumerBuilder<T> WithConsumer(Assembly assembly, bool includeDerivedMessageTypes = true)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));

            var allConsumers = assembly.GetTypes()
                .Where(x => x.IsClass && !x.IsAbstract && x.IsVisible)
                .SelectMany(x => x.GetInterfaces().Select(i => (ConsumerType: x, ConsumerInterface: i))
                .Where(x => x.ConsumerInterface.IsGenericType && x.ConsumerInterface.GetGenericTypeDefinition() == typeof(IConsumer<>)))
                .ToList();

            var consumers = allConsumers.Where(x => x.ConsumerInterface.GetGenericArguments().Single() == ConsumerSettings.MessageType).ToList();

            if (consumers.Count == 0)
            {
                throw new ConfigurationMessageBusException($"No consumer type could be found for MessageType {ConsumerSettings.MessageType} in the assembly {assembly}");
            }

            if (consumers.Count > 1)
            {
                throw new ConfigurationMessageBusException($"Multiple consumer types found for MessageType {ConsumerSettings.MessageType} in the assembly {assembly}. We can only have one consumer for a given message type.");
            }

            ConsumerSettings.ConsumerMode = ConsumerMode.Consumer;
            ConsumerSettings.ConsumerType = consumers[0].ConsumerType;
            SetupConsumerOnHandleMethod(ConsumerSettings);

            ConsumerSettings.ConsumersByMessageType.Add(typeof(T), ConsumerSettings);

            if (includeDerivedMessageTypes)
            {
                var derivedConsumers = allConsumers
                    .Where(x =>
                    {
                        // extract the T type from IConsumer<T>
                        var messageType = x.ConsumerInterface.GetGenericArguments().Single();
                        // find any IConsumer<T> where T is a derived type of the base message type (and not the base message type)
                        return messageType != ConsumerSettings.MessageType && ConsumerSettings.MessageType.IsAssignableFrom(messageType);
                    })
                    .Select(x => new MessageTypeConsumerInvokerSettings { MessageType = x.ConsumerInterface.GetGenericArguments().Single(), ConsumerType = x.ConsumerType })
                    .ToList();

                foreach (var derivedConsumer in derivedConsumers)
                {
                    SetupConsumerOnHandleMethod(derivedConsumer);

                    // ToDo: Check if this message Type is already registered
                    ConsumerSettings.ConsumersByMessageType.Add(derivedConsumer.MessageType, derivedConsumer);
                }
            }

            return this;
        }

        /// <summary>
        /// Declares type <typeparamref name="TConsumer"/> as the consumer of messages <typeparamref name="TMessage"/>.
        /// </summary>
        /// <typeparam name="TConsumer"></typeparam>
        /// <param name="method">Specifies how to delegate messages to the consumer type.</param>
        /// <returns></returns>
        public ConsumerBuilder<T> WithConsumer<TConsumer>(Func<TConsumer, T, string, Task> consumerMethod)
            where TConsumer : class
        {
            if (consumerMethod == null) throw new ArgumentNullException(nameof(consumerMethod));

            ConsumerSettings.ConsumerMode = ConsumerMode.Consumer;
            ConsumerSettings.ConsumerType = typeof(TConsumer);
            ConsumerSettings.ConsumerMethod = (consumer, message, path) => consumerMethod((TConsumer)consumer, (T)message, path);

            ConsumerSettings.ConsumersByMessageType.Add(typeof(T), ConsumerSettings);

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

            if (consumerMethodName == null)
            {
                consumerMethodName = nameof(IConsumer<object>.OnHandle);
            }

            ConsumerSettings.ConsumerMode = ConsumerMode.Consumer;
            ConsumerSettings.ConsumerType = consumerType;
            SetupConsumerOnHandleMethod(ConsumerSettings, consumerMethodName);

            ConsumerSettings.ConsumersByMessageType.Add(typeof(T), ConsumerSettings);

            return this;
        }

        private static void SetupConsumerOnHandleMethod(IMessageTypeConsumerInvokerSettings consumerInvokerSettings, string methodName = null)
        {
            if (consumerInvokerSettings == null) throw new ArgumentNullException(nameof(consumerInvokerSettings));

            if (methodName == null)
            {
                methodName = nameof(IConsumer<object>.OnHandle);
            }

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

            if (numArgs == 2)
            {
                consumerInvokerSettings.ConsumerMethod = (consumer, message, path) => (Task)consumerOnHandleMethod.Invoke(consumer, new[] { message, path });
            }
            else
            {
                consumerInvokerSettings.ConsumerMethod = (consumer, message, path) => (Task)consumerOnHandleMethod.Invoke(consumer, new[] { message });
            }
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
        /// Adds custom hooks for the consumer.
        /// </summary>
        /// <param name="eventsConfig"></param>
        /// <returns></returns>
        public ConsumerBuilder<T> AttachEvents(Action<IConsumerEvents> eventsConfig)
            => AttachEvents<ConsumerBuilder<T>>(eventsConfig);

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

        public ConsumerBuilder<T> Do(Action<ConsumerBuilder<T>> action) => base.Do(action);
    }
}