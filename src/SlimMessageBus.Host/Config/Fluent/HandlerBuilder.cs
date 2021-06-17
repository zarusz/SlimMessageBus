namespace SlimMessageBus.Host.Config
{
    using System;

    public class HandlerBuilder<TRequest, TResponse> : AbstractConsumerBuilder<TRequest>
    {
        public HandlerBuilder(MessageBusSettings settings)
            : base(settings)
        {
        }

        /// <summary>
        /// Configure topic name that incoming requests (<see cref="TRequest"/>) are expected on.
        /// </summary>
        /// <param name="topic">Topic name</param>
        /// <returns></returns>
        public TopicHandlerBuilder<TRequest, TResponse> Topic(string topic)
        {
            return new TopicHandlerBuilder<TRequest, TResponse>(topic, Settings);
        }

        /// <summary>
        /// Configure topic name that incoming requests (<see cref="TRequest"/>) are expected on.
        /// </summary>
        /// <param name="topic">Topic name</param>
        /// <param name="topicConfig"></param>
        /// <returns></returns>
        public TopicHandlerBuilder<TRequest, TResponse> Topic(string topic, Action<TopicHandlerBuilder<TRequest, TResponse>> topicConfig)
        {
            var b = Topic(topic);
            topicConfig(b);
            return b;
        }
    }
}