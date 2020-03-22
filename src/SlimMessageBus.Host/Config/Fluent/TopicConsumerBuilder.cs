using System;
using System.Threading.Tasks;

namespace SlimMessageBus.Host.Config
{
    public class TopicConsumerBuilder<TMessage> : AbstractTopicConsumerBuilder
    {
        public TopicConsumerBuilder(string topic, Type messageType, MessageBusSettings settings)
            : base(topic, messageType, settings)
        {
        }

        /// <summary>
        /// Declares type <typeparamref name="TConsumer"/> as the consumer of messages <typeparamref name="TMessage"/>.
        /// The consumer type has to implement <see cref="IConsumer{TMessage}"/> interface.
        /// </summary>
        /// <typeparam name="TConsumer"></typeparam>
        /// <returns></returns>
        public TopicConsumerBuilder<TMessage> WithConsumer<TConsumer>()
            where TConsumer : class, IConsumer<TMessage>
        {
            ConsumerSettings.ConsumerType = typeof(TConsumer);
            ConsumerSettings.ConsumerMode = ConsumerMode.Consumer;
            ConsumerSettings.ConsumerMethod = (consumer, message, name) => ((TConsumer)consumer).OnHandle((TMessage)message, name);

            return this;
        }

        /// <summary>
        /// Declares type <typeparamref name="TConsumer"/> as the consumer of messages <typeparamref name="TMessage"/>.
        /// </summary>
        /// <typeparam name="TConsumer"></typeparam>
        /// <param name="method">Specifies how to delegate messages to the consumer type.</param>
        /// <returns></returns>
        public TopicConsumerBuilder<TMessage> WithConsumer<TConsumer>(Func<TConsumer, TMessage, string, Task> consumerMethod)
            where TConsumer : class
        {
            if (consumerMethod == null) throw new ArgumentNullException(nameof(consumerMethod));

            ConsumerSettings.ConsumerType = typeof(TConsumer);
            ConsumerSettings.ConsumerMode = ConsumerMode.Consumer;
            ConsumerSettings.ConsumerMethod = (consumer, message, name) => consumerMethod((TConsumer)consumer, (TMessage)message, name);

            return this;
        }

        /// <summary>
        /// Declares type <typeparamref name="TConsumer"/> as the consumer of messages <typeparamref name="TMessage"/>.
        /// The consumer type has to have a method: <see cref="Task"/> <paramref name="consumerMethodName"/>(<typeparamref name="TMessage"/>, <see cref="string"/>).
        /// </summary>
        /// <typeparam name="TConsumer"></typeparam>
        /// <param name="consumerMethodName"></param>
        /// <returns></returns>
        public TopicConsumerBuilder<TMessage> WithConsumer<TConsumer>(string consumerMethodName)
            where TConsumer : class
        {
            if (consumerMethodName == null) throw new ArgumentNullException(nameof(consumerMethodName));

            return WithConsumer(typeof(TConsumer), consumerMethodName);
        }

        /// <summary>
        /// Declares type <typeparamref name="TConsumer"/> as the consumer of messages <typeparamref name="TMessage"/>.
        /// The consumer type has to have a method: <see cref="Task"/> <paramref name="methodName"/>(<typeparamref name="TMessage"/>, <see cref="string"/>).
        /// </summary>
        /// <param name="consumerType"></param>
        /// <param name="methodName">If null, will default to <see cref="IConsumer{TMessage}.OnHandle(TMessage, string)"/> </param>
        /// <returns></returns>
        public TopicConsumerBuilder<TMessage> WithConsumer(Type consumerType, string methodName = null)
        {
            if (methodName == null)
            {
                methodName = nameof(IConsumer<object>.OnHandle);
            }

            /// See <see cref="IConsumer{TMessage}.OnHandle(TMessage, string)"/> and <see cref="IRequestHandler{TRequest, TResponse}.OnHandle(TRequest, string)"/> 
            var numArgs = 2;
            // try to see if two param method exists
            var consumerOnHandleMethod = consumerType.GetMethod(methodName, new[] { ConsumerSettings.MessageType, typeof(string) });
            if (consumerOnHandleMethod == null)
            {
                // try to see if one param method exists
                numArgs = 1;
                consumerOnHandleMethod = consumerType.GetMethod(methodName, new[] { ConsumerSettings.MessageType });

                Assert.IsNotNull(consumerOnHandleMethod,
                    () => new ConfigurationMessageBusException($"Consumer type {consumerType} validation error: the method {methodName} with parameters of type {ConsumerSettings.MessageType} and {typeof(string)} was not found."));
            }

            // ensure the method returns a Task or Task<T>
            Assert.IsTrue(typeof(Task).IsAssignableFrom(consumerOnHandleMethod.ReturnType),
                () => new ConfigurationMessageBusException($"Consumer type {consumerType} validation error: the response type of method {methodName} must return {typeof(Task)}"));

            ConsumerSettings.ConsumerType = consumerType;
            ConsumerSettings.ConsumerMode = ConsumerMode.Consumer;
            if (numArgs == 2)
            {
                ConsumerSettings.ConsumerMethod = (consumer, message, name) => (Task)consumerOnHandleMethod.Invoke(consumer, new[] { message, name });
            }
            else
            {
                ConsumerSettings.ConsumerMethod = (consumer, message, name) => (Task)consumerOnHandleMethod.Invoke(consumer, new[] { message });
            }

            return this;
        }

        /// <summary>
        /// Number of concurrent competing consumer instances that the bus is asking for the DI plugin.
        /// This dictates how many concurrent messages can be processed at a time.
        /// </summary>
        /// <param name="numberOfInstances"></param>
        /// <returns></returns>
        public TopicConsumerBuilder<TMessage> Instances(int numberOfInstances)
        {
            ConsumerSettings.Instances = numberOfInstances;
            return this;
        }
    }
}