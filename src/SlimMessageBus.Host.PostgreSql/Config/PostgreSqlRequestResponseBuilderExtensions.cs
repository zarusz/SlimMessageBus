namespace SlimMessageBus.Host.PostgreSql;

public static class PostgreSqlRequestResponseBuilderExtensions
{
    public static RequestResponseBuilder ReplyToQueue(this RequestResponseBuilder builder, string queue)
        => RelationalBuilderExtensions.ReplyToQueue(builder, queue);

    public static RequestResponseBuilder ReplyToTopic(this RequestResponseBuilder builder, string topic, string subscriptionName)
        => RelationalBuilderExtensions.ReplyToTopic(builder, topic, subscriptionName, PostgreSqlConsumerBuilderExtensions.PropertySubscriptionName);
}
