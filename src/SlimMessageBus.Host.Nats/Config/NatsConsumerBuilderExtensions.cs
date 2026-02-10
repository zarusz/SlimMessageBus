namespace SlimMessageBus.Host.Nats;

public static class NatsConsumerBuilderExtensions
{
    public static ConsumerBuilder<T> Queue<T>(this ConsumerBuilder<T> natsBuilder, string natsQueue)
    {
        if (natsBuilder is null) throw new ArgumentNullException(nameof(natsBuilder));
        if (natsQueue is null) throw new ArgumentNullException(nameof(natsQueue));

        natsBuilder.Path(natsQueue);
        natsBuilder.ConsumerSettings.PathKind = PathKind.Queue;
        return natsBuilder;
    }

    public static ConsumerBuilder<T> Queue<T>(this ConsumerBuilder<T> natsBuilder, string natsQueue, Action<ConsumerBuilder<T>> natsTopicConfig)
    {
        if (natsBuilder is null) throw new ArgumentNullException(nameof(natsBuilder));
        if (natsTopicConfig is null) throw new ArgumentNullException(nameof(natsTopicConfig));

        var b = natsBuilder.Queue(natsQueue);
        natsTopicConfig(b);
        return b;
    }
}