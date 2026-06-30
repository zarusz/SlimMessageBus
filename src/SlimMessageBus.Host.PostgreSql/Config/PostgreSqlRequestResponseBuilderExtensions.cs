namespace SlimMessageBus.Host.PostgreSql;

public static class PostgreSqlRequestResponseBuilderExtensions
{
    public static RequestResponseBuilder ReplyToQueue(this RequestResponseBuilder builder, string queue)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (queue is null) throw new ArgumentNullException(nameof(queue));

        builder.Settings.Path = queue;
        builder.Settings.PathKind = PathKind.Queue;
        return builder;
    }

    public static RequestResponseBuilder ReplyToTopic(this RequestResponseBuilder builder, string topic, string subscriptionName)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (topic is null) throw new ArgumentNullException(nameof(topic));
        if (subscriptionName is null) throw new ArgumentNullException(nameof(subscriptionName));

        builder.Settings.Path = topic;
        builder.Settings.PathKind = PathKind.Topic;
        builder.Settings.Properties[PostgreSqlConsumerBuilderExtensions.PropertySubscriptionName] = subscriptionName;
        return builder;
    }
}
