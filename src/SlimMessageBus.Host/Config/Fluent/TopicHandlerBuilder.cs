using System.Linq;

namespace SlimMessageBus.Host.Config
{
    public class TopicHandlerBuilder<TRequest, TResponse> : TopicConsumerBuilder<TRequest>
        where TRequest : IRequestMessage<TResponse>
    {
        public TopicHandlerBuilder(string topic, MessageBusSettings settings)
            : base(topic, typeof(TRequest), settings)
        {
            ConsumerSettings.ResponseType = typeof(TResponse);

            var consumerSettingsExist = settings.Consumers.Any(x => x.Topic == topic && x.ConsumerMode == ConsumerMode.RequestResponse);
            Assert.IsFalse(consumerSettingsExist,
                () => new ConfigurationMessageBusException($"Attempted to configure request handler for topic '{topic}' when one was already configured. You can only have one request handler for a given topic, otherwise which response would you send back?"));
        }

        public TopicHandlerBuilder<TRequest, TResponse> WithHandler<THandler>()
            where THandler : IRequestHandler<TRequest, TResponse>
        {
            ConsumerSettings.ConsumerType = typeof(THandler);
            ConsumerSettings.ConsumerMode = ConsumerMode.RequestResponse;
            return this;
        }

        public TopicHandlerBuilder<TRequest, TResponse> Instances(int numberOfInstances)
        {
            ConsumerSettings.Instances = numberOfInstances;
            return this;
        }
    }
}