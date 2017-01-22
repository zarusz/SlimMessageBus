namespace SlimMessageBus.Host.Config
{
    public class GroupHandlerBuilder<TRequest, TResponse> : GroupConsumerBuilder<TRequest> 
        where TRequest : IRequestMessage<TResponse>
    {
        public GroupHandlerBuilder(string group, string topic, MessageBusSettings settings)
            : base(group, topic, settings)
        {
            ConsumerSettings.ResponseType = typeof (TResponse);
        }

        public GroupHandlerBuilder<TRequest, TResponse> WithHandler<THandler>()
            where THandler : IRequestHandler<TRequest, TResponse>
        {
            ConsumerSettings.ConsumerType = typeof(THandler);
            ConsumerSettings.ConsumerMode = ConsumerMode.RequestResponse;
            return this;
        }

        public GroupHandlerBuilder<TRequest, TResponse> Instances(int numberOfInstances)
        {
            ConsumerSettings.Instances = numberOfInstances;
            return this;
        }
    }
}