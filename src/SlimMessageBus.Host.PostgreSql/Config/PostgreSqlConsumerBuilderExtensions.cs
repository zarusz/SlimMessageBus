namespace SlimMessageBus.Host.PostgreSql;

public static class PostgreSqlConsumerBuilderExtensions
{
    internal const string PropertySubscriptionName = "PostgreSql_SubscriptionName";

    public static ConsumerBuilder<T> Queue<T>(this ConsumerBuilder<T> builder, string queue)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (queue is null) throw new ArgumentNullException(nameof(queue));

        builder.Path(queue);
        builder.ConsumerSettings.PathKind = PathKind.Queue;
        return builder;
    }

    public static ConsumerBuilder<T> Topic<T>(this ConsumerBuilder<T> builder, string topic, string subscriptionName)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (topic is null) throw new ArgumentNullException(nameof(topic));
        if (subscriptionName is null) throw new ArgumentNullException(nameof(subscriptionName));

        builder.Topic(topic);
        builder.ConsumerSettings.PathKind = PathKind.Topic;
        builder.ConsumerSettings.Properties[PropertySubscriptionName] = subscriptionName;
        return builder;
    }

    public static ConsumerBuilder<T> Subscription<T>(this ConsumerBuilder<T> builder, string subscriptionName)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (subscriptionName is null) throw new ArgumentNullException(nameof(subscriptionName));

        builder.ConsumerSettings.Properties[PropertySubscriptionName] = subscriptionName;
        return builder;
    }

    internal static string GetSubscriptionName(this AbstractConsumerSettings settings)
        => settings.Properties.TryGetValue(PropertySubscriptionName, out var value)
            ? (string)value
            : settings.Path;
}
