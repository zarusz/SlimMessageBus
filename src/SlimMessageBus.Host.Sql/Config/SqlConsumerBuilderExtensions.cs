namespace SlimMessageBus.Host.Sql;

public static class SqlConsumerBuilderExtensions
{
    internal const string PropertySubscriptionName = "Sql_SubscriptionName";

    public static ConsumerBuilder<T> Queue<T>(this ConsumerBuilder<T> builder, string queue)
        => RelationalBuilderExtensions.Queue(builder, queue);

    public static ConsumerBuilder<T> Topic<T>(this ConsumerBuilder<T> builder, string topic, string subscriptionName)
        => RelationalBuilderExtensions.Topic(builder, topic, subscriptionName, PropertySubscriptionName);

    public static ConsumerBuilder<T> Subscription<T>(this ConsumerBuilder<T> builder, string subscriptionName)
        => RelationalBuilderExtensions.Subscription(builder, subscriptionName, PropertySubscriptionName);

    internal static string GetSubscriptionName(this AbstractConsumerSettings settings)
        => RelationalBuilderExtensions.GetSubscriptionName(settings, PropertySubscriptionName);
}
