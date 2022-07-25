namespace SlimMessageBus.Host.Config
{
    using System;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Threading.Tasks;

    public class HandlerBuilder<TRequest, TResponse> : AbstractConsumerBuilder
    {
        public HandlerBuilder(MessageBusSettings settings, Type requestType = null, Type responseType = null)
            : base(settings, requestType ?? typeof(TRequest))
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            ConsumerSettings.ResponseType = responseType ?? typeof(TResponse);
        }

        /// <summary>
        /// Configure topic name (or queue name) that incoming requests (<see cref="TRequest"/>) are expected on.
        /// </summary>
        /// <param name="path">Topic name</param>
        /// <returns></returns>
        public HandlerBuilder<TRequest, TResponse> Topic(string path) => Path(path);

        /// <summary>
        /// Configure topic name (or queue name) that incoming requests (<see cref="TRequest"/>) are expected on.
        /// </summary>
        /// <param name="path">Topic name</param>
        /// <returns></returns>
        public HandlerBuilder<TRequest, TResponse> Path(string path)
        {
            var consumerSettingsExist = Settings.Consumers.Any(x => x.Path == path && x.ConsumerMode == ConsumerMode.RequestResponse);
            Assert.IsFalse(consumerSettingsExist,
                () => new ConfigurationMessageBusException($"Attempted to configure request handler for topic/queue '{path}' when one was already configured. You can only have one request handler for a given topic/queue, otherwise which response would you send back?"));

            ConsumerSettings.Path = path;
            return this;
        }

        /// <summary>
        /// Configure topic name that incoming requests (<see cref="TRequest"/>) are expected on.
        /// </summary>
        /// <param name="topic">Topic name</param>
        /// <param name="topicConfig"></param>
        /// <returns></returns>
        public HandlerBuilder<TRequest, TResponse> Path(string path, Action<HandlerBuilder<TRequest, TResponse>> pathConfig)
        {
            if (pathConfig is null) throw new ArgumentNullException(nameof(pathConfig));

            var b = Path(path);
            pathConfig(b);
            return b;
        }
        /// <summary>
        /// Configure topic name that incoming requests (<see cref="TRequest"/>) are expected on.
        /// </summary>
        /// <param name="topic">Topic name</param>
        /// <param name="topicConfig"></param>
        /// <returns></returns>
        public HandlerBuilder<TRequest, TResponse> Topic(string topic, Action<HandlerBuilder<TRequest, TResponse>> topicConfig) => Path(topic, topicConfig);

        public HandlerBuilder<TRequest, TResponse> WithHandler<THandler>()
            where THandler : IRequestHandler<TRequest, TResponse>
        {
            Assert.IsNotNull(ConsumerSettings.ResponseType,
                () => new ConfigurationMessageBusException($"The {nameof(ConsumerSettings)}.{nameof(ConsumerSettings.ResponseType)} is not set"));

            ConsumerSettings.ConsumerMode = ConsumerMode.RequestResponse;
            ConsumerSettings.ConsumerType = typeof(THandler);
            ConsumerSettings.ConsumerMethod = (consumer, message, path) => ((THandler)consumer).OnHandle((TRequest)message, path);
            ConsumerSettings.ConsumerMethodResult = (task) => ((Task<TResponse>)task).Result;

            ConsumerSettings.ConsumersByMessageType.Add(MessageType, ConsumerSettings);

            return this;
        }

        public HandlerBuilder<TRequest, TResponse> WithHandler(Type handlerType)
        {
            Assert.IsNotNull(ConsumerSettings.ResponseType,
                () => new ConfigurationMessageBusException($"The {nameof(ConsumerSettings)}.{nameof(ConsumerSettings.ResponseType)} is not set"));

            ConsumerSettings.ConsumerMode = ConsumerMode.RequestResponse;
            ConsumerSettings.ConsumerType = handlerType;
            SetupConsumerOnHandleMethod(ConsumerSettings);

            var taskOfResponseTypeResultPropertyInfo = typeof(Task<>).MakeGenericType(ConsumerSettings.ResponseType).GetProperty(nameof(Task<object>.Result));
            ConsumerSettings.ConsumerMethodResult = ReflectionUtils.GenerateGetterFunc(taskOfResponseTypeResultPropertyInfo);

            ConsumerSettings.ConsumersByMessageType.Add(MessageType, ConsumerSettings);

            return this;
        }

        public HandlerBuilder<TRequest, TResponse> Instances(int numberOfInstances)
        {
            ConsumerSettings.Instances = numberOfInstances;
            return this;
        }

        /// <summary>
        /// Adds custom hooks for the handler.
        /// </summary>
        /// <param name="eventsConfig"></param>
        /// <returns></returns>
        public HandlerBuilder<TRequest, TResponse> AttachEvents(Action<IConsumerEvents> eventsConfig)
            => AttachEvents<HandlerBuilder<TRequest, TResponse>>(eventsConfig);

        public HandlerBuilder<TRequest, TResponse> Do(Action<HandlerBuilder<TRequest, TResponse>> action) => base.Do(action);
    }
}