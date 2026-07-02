namespace SlimMessageBus.Host.Sql;

public static class SqlHandlerBuilderExtensions
{
    public static HandlerBuilder<TRequest, TResponse> Queue<TRequest, TResponse>(this HandlerBuilder<TRequest, TResponse> builder, string queue)
        => RelationalBuilderExtensions.Queue(builder, queue);

    public static HandlerBuilder<TRequest> Queue<TRequest>(this HandlerBuilder<TRequest> builder, string queue)
        => RelationalBuilderExtensions.Queue(builder, queue);

    public static HandlerBuilder<TRequest, TResponse> Topic<TRequest, TResponse>(this HandlerBuilder<TRequest, TResponse> builder, string topic, string subscriptionName)
        => RelationalBuilderExtensions.Topic(builder, topic, subscriptionName, SqlConsumerBuilderExtensions.PropertySubscriptionName);

    public static HandlerBuilder<TRequest> Topic<TRequest>(this HandlerBuilder<TRequest> builder, string topic, string subscriptionName)
        => RelationalBuilderExtensions.Topic(builder, topic, subscriptionName, SqlConsumerBuilderExtensions.PropertySubscriptionName);
}
