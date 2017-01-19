using System;

namespace SlimMessageBus.Host.Config
{
    public class HandlerBuilder<TRequest, TResponse> : ConsumerBuilder<TRequest>
        where TRequest : IRequestMessage<TResponse> 
    {
        public HandlerBuilder(MessageBusSettings settings)
            : base(settings)
        {
        }

        public TopicHandlerBuilder<TRequest, TResponse> Topic(string topic)
        {
            return new TopicHandlerBuilder<TRequest, TResponse>(topic, Settings);
        }

        public TopicHandlerBuilder<TRequest, TResponse> Topic(string topic, Action<TopicHandlerBuilder<TRequest, TResponse>> topicConfig)
        {
            var b = Topic(topic);
            topicConfig(b);
            return b;
        }
    }
}