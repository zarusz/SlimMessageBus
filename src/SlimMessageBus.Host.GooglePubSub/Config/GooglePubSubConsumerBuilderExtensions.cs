namespace SlimMessageBus.Host.GooglePubSub;

public static class GooglePubSubConsumerBuilderExtensions
{
    public static TBuilder SubscriptionName<TBuilder>(this TBuilder builder, string subscriptionName)
        where TBuilder : IAbstractConsumerBuilder
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (subscriptionName is null) throw new ArgumentNullException(nameof(subscriptionName));

        builder.ConsumerSettings.SetSubscriptionName(subscriptionName);
        return builder;
    }

    public static TBuilder CreateTopicOptions<TBuilder>(this TBuilder builder, Action<Topic> configure)
        where TBuilder : IAbstractConsumerBuilder
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        GooglePubSubProperties.CreateTopicOptions.Set(builder.ConsumerSettings, configure);
        return builder;
    }

    public static TBuilder CreateSubscriptionOptions<TBuilder>(this TBuilder builder, Action<Subscription> configure)
        where TBuilder : IAbstractConsumerBuilder
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        GooglePubSubProperties.CreateSubscriptionOptions.Set(builder.ConsumerSettings, configure);
        return builder;
    }
}
