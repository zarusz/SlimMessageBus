namespace SlimMessageBus.Host.PostgreSql;

public static class PostgreSqlHandlerBuilderExtensions
{
    public static HandlerBuilder<TRequest, TResponse> Queue<TRequest, TResponse>(this HandlerBuilder<TRequest, TResponse> builder, string queue)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (queue is null) throw new ArgumentNullException(nameof(queue));

        builder.Path(queue);
        builder.ConsumerSettings.PathKind = PathKind.Queue;
        return builder;
    }

    public static HandlerBuilder<TRequest> Queue<TRequest>(this HandlerBuilder<TRequest> builder, string queue)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (queue is null) throw new ArgumentNullException(nameof(queue));

        builder.Path(queue);
        builder.ConsumerSettings.PathKind = PathKind.Queue;
        return builder;
    }

    public static HandlerBuilder<TRequest, TResponse> Topic<TRequest, TResponse>(this HandlerBuilder<TRequest, TResponse> builder, string topic, string subscriptionName)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (topic is null) throw new ArgumentNullException(nameof(topic));
        if (subscriptionName is null) throw new ArgumentNullException(nameof(subscriptionName));

        builder.Path(topic);
        builder.ConsumerSettings.PathKind = PathKind.Topic;
        builder.ConsumerSettings.Properties[PostgreSqlConsumerBuilderExtensions.PropertySubscriptionName] = subscriptionName;
        return builder;
    }

    public static HandlerBuilder<TRequest> Topic<TRequest>(this HandlerBuilder<TRequest> builder, string topic, string subscriptionName)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (topic is null) throw new ArgumentNullException(nameof(topic));
        if (subscriptionName is null) throw new ArgumentNullException(nameof(subscriptionName));

        builder.Path(topic);
        builder.ConsumerSettings.PathKind = PathKind.Topic;
        builder.ConsumerSettings.Properties[PostgreSqlConsumerBuilderExtensions.PropertySubscriptionName] = subscriptionName;
        return builder;
    }
}
