namespace SlimMessageBus.Host.Config
{
    public class TopicHandlerBuilder<TRequest, TResponse> : TopicConsumerBuilder<TRequest>
        where TRequest : IRequestMessage<TResponse>
    {
        public TopicHandlerBuilder(string topic, MessageBusSettings settings)
            : base(topic, typeof(TRequest), settings)
        {
        }

        /// <summary>
        /// Configure the consumer group (GroupId) to use for the handler.
        /// </summary>
        /// <param name="group">Consumer group</param>
        /// <returns></returns>
        public GroupHandlerBuilder<TRequest, TResponse> Group(string group)
        {
            return new GroupHandlerBuilder<TRequest, TResponse>(group, Topic, Settings);
        }
    }
}