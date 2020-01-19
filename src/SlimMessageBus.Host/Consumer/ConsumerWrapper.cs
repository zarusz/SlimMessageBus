using System;
using System.Reflection;
using System.Threading.Tasks;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host
{
    /// <summary>
    /// Wrapper for the Consumer method (<see cref="IConsumer{TMessage}.OnHandle(TMessage, string)"/> or <see cref="IRequestHandler{TRequest, TResponse}.OnHandle(TRequest, string)"/>) and the return Task type.
    /// It uses refrection to wrap the method.
    /// </summary>
    public class ConsumerWrapper
    {
        public ConsumerSettings ConsumerSettings { get; }

        /// <summary>
        /// The reflection cache OnHandle method.
        /// </summary>
        public MethodInfo ConsumerOnHandleMethod { get; }

        /// <summary>
        /// The reflection cache <see cref="Task{TResult}.Result"/> property that corresponds to handler return type.
        /// </summary>
        public PropertyInfo TaskResultProperty { get; }

        public ConsumerWrapper(ConsumerSettings consumerSettings)
        {
            ConsumerSettings = consumerSettings ?? throw new ArgumentNullException(nameof(consumerSettings));

            Assert.IsNotNull(consumerSettings.ConsumerType,
                () => new ConfigurationMessageBusException($"The {nameof(ConsumerSettings)}.{nameof(consumerSettings.ConsumerType)} is not set"));
            Assert.IsNotNull(consumerSettings.MessageType,
                () => new ConfigurationMessageBusException($"The {nameof(ConsumerSettings)}.{nameof(consumerSettings.MessageType)} is not set"));

            /// See <see cref="IConsumer{TMessage}.OnHandle(TMessage, string)"/> and <see cref="IRequestHandler{TRequest, TResponse}.OnHandle(TRequest, string)"/> 
            ConsumerOnHandleMethod = consumerSettings.ConsumerType.GetMethod(nameof(IConsumer<object>.OnHandle), new[] { consumerSettings.MessageType, typeof(string) });

            if (consumerSettings.ConsumerMode == ConsumerMode.RequestResponse)
            {
                Assert.IsNotNull(consumerSettings.ResponseType,
                    () => new ConfigurationMessageBusException($"The {nameof(ConsumerSettings)}.{nameof(consumerSettings.ResponseType)} is not set"));

                /// See <see cref="IRequestHandler{TRequest, TResponse}.OnHandle(TRequest, string)"/>
                var taskType = typeof(Task<>).MakeGenericType(consumerSettings.ResponseType);
                TaskResultProperty = taskType.GetProperty(nameof(Task<object>.Result));
            }
        }

        public Task OnHandle(object consumerInstance, object message)
        {
            return (Task)ConsumerOnHandleMethod.Invoke(consumerInstance, new[] { message, ConsumerSettings.Topic });
        }

        public object GetResponseValue(Task task)
        {
            return TaskResultProperty.GetValue(task);
        }
    }
}