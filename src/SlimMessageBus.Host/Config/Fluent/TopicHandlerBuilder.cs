namespace SlimMessageBus.Host.Config
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    public class TopicHandlerBuilder<TRequest, TResponse> : AbstractTopicConsumerBuilder
    {
        public TopicHandlerBuilder(string path, MessageBusSettings settings)
            : base(path, typeof(TRequest), settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            ConsumerSettings.ResponseType = typeof(TResponse);

            var consumerSettingsExist = settings.Consumers.Any(x => x.Path == path && x.ConsumerMode == ConsumerMode.RequestResponse);
            Assert.IsFalse(consumerSettingsExist,
                () => new ConfigurationMessageBusException($"Attempted to configure request handler for topic '{path}' when one was already configured. You can only have one request handler for a given topic, otherwise which response would you send back?"));
        }

        public TopicHandlerBuilder<TRequest, TResponse> WithHandler<THandler>()
            where THandler : IRequestHandler<TRequest, TResponse>
        {
            Assert.IsNotNull(ConsumerSettings.ResponseType,
                () => new ConfigurationMessageBusException($"The {nameof(ConsumerSettings)}.{nameof(ConsumerSettings.ResponseType)} is not set"));

            ConsumerSettings.ConsumerMode = ConsumerMode.RequestResponse;
            ConsumerSettings.ConsumerType = typeof(THandler);
            ConsumerSettings.ConsumerMethod = (consumer, message, name) => ((THandler)consumer).OnHandle((TRequest)message, name);
            ConsumerSettings.ConsumerMethodResult = (task) => ((Task<TResponse>)task).Result;

            return this;
        }

        public TopicHandlerBuilder<TRequest, TResponse> Instances(int numberOfInstances)
        {
            ConsumerSettings.Instances = numberOfInstances;
            return this;
        }

        /// <summary>
        /// Adds custom hooks for the handler.
        /// </summary>
        /// <param name="eventsConfig"></param>
        /// <returns></returns>
        public TopicHandlerBuilder<TRequest, TResponse> AttachEvents(Action<IConsumerEvents> eventsConfig)
            => AttachEvents<TopicHandlerBuilder<TRequest, TResponse>>(eventsConfig);

        public TopicHandlerBuilder<TRequest, TResponse> Do(Action<TopicHandlerBuilder<TRequest, TResponse>> action) => base.Do(action);
    }
}