namespace SlimMessageBus.Host.Relational;

public static class RelationalBuilderExtensions
{
    public static ConsumerBuilder<T> Queue<T>(ConsumerBuilder<T> builder, string queue)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (queue is null) throw new ArgumentNullException(nameof(queue));

        builder.Path(queue);
        builder.ConsumerSettings.PathKind = PathKind.Queue;
        return builder;
    }

    public static ConsumerBuilder<T> Topic<T>(ConsumerBuilder<T> builder, string topic, string subscriptionName, string subscriptionPropertyName)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (topic is null) throw new ArgumentNullException(nameof(topic));

        builder.Topic(topic);
        builder.ConsumerSettings.PathKind = PathKind.Topic;
        SetSubscription(builder.ConsumerSettings, subscriptionName, subscriptionPropertyName);
        return builder;
    }

    public static ConsumerBuilder<T> Subscription<T>(ConsumerBuilder<T> builder, string subscriptionName, string subscriptionPropertyName)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        SetSubscription(builder.ConsumerSettings, subscriptionName, subscriptionPropertyName);
        return builder;
    }

    public static HandlerBuilder<TRequest, TResponse> Queue<TRequest, TResponse>(HandlerBuilder<TRequest, TResponse> builder, string queue)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (queue is null) throw new ArgumentNullException(nameof(queue));

        builder.Path(queue);
        builder.ConsumerSettings.PathKind = PathKind.Queue;
        return builder;
    }

    public static HandlerBuilder<TRequest> Queue<TRequest>(HandlerBuilder<TRequest> builder, string queue)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (queue is null) throw new ArgumentNullException(nameof(queue));

        builder.Path(queue);
        builder.ConsumerSettings.PathKind = PathKind.Queue;
        return builder;
    }

    public static HandlerBuilder<TRequest, TResponse> Topic<TRequest, TResponse>(HandlerBuilder<TRequest, TResponse> builder, string topic, string subscriptionName, string subscriptionPropertyName)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (topic is null) throw new ArgumentNullException(nameof(topic));

        builder.Path(topic);
        builder.ConsumerSettings.PathKind = PathKind.Topic;
        SetSubscription(builder.ConsumerSettings, subscriptionName, subscriptionPropertyName);
        return builder;
    }

    public static HandlerBuilder<TRequest> Topic<TRequest>(HandlerBuilder<TRequest> builder, string topic, string subscriptionName, string subscriptionPropertyName)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (topic is null) throw new ArgumentNullException(nameof(topic));

        builder.Path(topic);
        builder.ConsumerSettings.PathKind = PathKind.Topic;
        SetSubscription(builder.ConsumerSettings, subscriptionName, subscriptionPropertyName);
        return builder;
    }

    public static ProducerBuilder<T> DefaultQueue<T>(ProducerBuilder<T> producerBuilder, string queue)
    {
        if (producerBuilder is null) throw new ArgumentNullException(nameof(producerBuilder));
        if (queue is null) throw new ArgumentNullException(nameof(queue));

        producerBuilder.DefaultTopic(queue);
        return ToQueue(producerBuilder);
    }

    public static ProducerBuilder<T> ToTopic<T>(ProducerBuilder<T> producerBuilder)
        => SetProducerPathKind(producerBuilder, PathKind.Topic);

    public static ProducerBuilder<T> ToQueue<T>(ProducerBuilder<T> producerBuilder)
        => SetProducerPathKind(producerBuilder, PathKind.Queue);

    public static RequestResponseBuilder ReplyToQueue(RequestResponseBuilder builder, string queue)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (queue is null) throw new ArgumentNullException(nameof(queue));

        builder.Settings.Path = queue;
        builder.Settings.PathKind = PathKind.Queue;
        return builder;
    }

    public static RequestResponseBuilder ReplyToTopic(RequestResponseBuilder builder, string topic, string subscriptionName, string subscriptionPropertyName)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (topic is null) throw new ArgumentNullException(nameof(topic));

        builder.Settings.Path = topic;
        builder.Settings.PathKind = PathKind.Topic;
        SetSubscription(builder.Settings, subscriptionName, subscriptionPropertyName);
        return builder;
    }

    public static string GetSubscriptionName(AbstractConsumerSettings settings, string subscriptionPropertyName)
        => settings.Properties.TryGetValue(subscriptionPropertyName, out var value)
            ? (string)value
            : settings.Path;

    private static void SetSubscription(HasProviderExtensions settings, string subscriptionName, string subscriptionPropertyName)
    {
        if (subscriptionName is null) throw new ArgumentNullException(nameof(subscriptionName));

        settings.Properties[subscriptionPropertyName] = subscriptionName;
    }

    private static ProducerBuilder<T> SetProducerPathKind<T>(ProducerBuilder<T> producerBuilder, PathKind pathKind)
    {
        if (producerBuilder is null) throw new ArgumentNullException(nameof(producerBuilder));

        producerBuilder.Settings.PathKind = pathKind;
        return producerBuilder;
    }
}
