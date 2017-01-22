namespace SlimMessageBus.Host.Config
{
    public class TopicHandlerBuilder<TRequest, TResponse> : TopicConsumerBuilder<TRequest>
        where TRequest : IRequestMessage<TResponse>
    {
        public TopicHandlerBuilder(string topic, MessageBusSettings settings)
            : base(topic, settings)
        {
        }

        public GroupHandlerBuilder<TRequest, TResponse> Group(string group)
        {
            return new GroupHandlerBuilder<TRequest, TResponse>(group, Topic, Settings);
        }
    }
}