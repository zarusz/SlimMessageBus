namespace SlimMessageBus.Host.GooglePubSub;

public static class GooglePubSubProducerBuilderExtensions
{
    public static ProducerBuilder<T> WithModifier<T>(this ProducerBuilder<T> builder, GooglePubSubMessageModifier<T> modifier)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (modifier is null) throw new ArgumentNullException(nameof(modifier));

        GooglePubSubProperties.MessageModifier.Set(builder.Settings, (message, transportMessage) => modifier((T)message, transportMessage));
        return builder;
    }

    public static ProducerBuilder<T> CreateTopicOptions<T>(this ProducerBuilder<T> builder, Action<Topic> configure)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        GooglePubSubProperties.CreateTopicOptions.Set(builder.Settings, configure);
        return builder;
    }
}
