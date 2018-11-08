using System.Reflection;
using System.Threading.Tasks;

namespace SlimMessageBus.Host.Config
{
    public class ConsumerRuntimeInfo
    {
        public ConsumerSettings ConsumerSettings { get; }

        public MethodInfo ConsumerOnHandleMethod { get; }

        /// <summary>
        /// The <see cref="Task{TResult}.Result"/> property that corresponds to handler return type.
        /// </summary>
        public PropertyInfo TaskResultProperty { get; }

        public ConsumerRuntimeInfo(ConsumerSettings consumerSettings)
        {
            ConsumerSettings = consumerSettings;

            if (consumerSettings.ConsumerType == null)
            {
                throw new ConfigurationMessageBusException($"{nameof(consumerSettings.ConsumerType)} is not set on the {consumerSettings}");
            }
            if (consumerSettings.MessageType == null)
            {
                throw new ConfigurationMessageBusException($"{nameof(consumerSettings.MessageType)} is not set on the {consumerSettings}");
            }
            ConsumerOnHandleMethod = consumerSettings.ConsumerType.GetMethod(nameof(IConsumer<object>.OnHandle), new[] { consumerSettings.MessageType, typeof(string) });

            if (consumerSettings.ConsumerMode == ConsumerMode.RequestResponse)
            {
                if (consumerSettings.ResponseType == null)
                {
                    throw new ConfigurationMessageBusException($"{nameof(consumerSettings.ResponseType)} is not set on the {consumerSettings}");
                }

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