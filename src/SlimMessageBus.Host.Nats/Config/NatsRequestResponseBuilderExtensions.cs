namespace SlimMessageBus.Host.Nats;

public static class NatsRequestResponseBuilderExtensions
{
    public static RequestResponseBuilder ReplyToQueue(this RequestResponseBuilder natsBuilder, string natsQueue)
    {
        if (natsBuilder is null) throw new ArgumentNullException(nameof(natsBuilder));
        if (natsQueue is null) throw new ArgumentNullException(nameof(natsQueue));

        natsBuilder.Settings.Path = natsQueue;
        natsBuilder.Settings.PathKind = PathKind.Queue;
        return natsBuilder;
    }

    public static RequestResponseBuilder ReplyToQueue(this RequestResponseBuilder natsBuilder, string natsQueue, Action<RequestResponseBuilder> natsBuilderConfig)
    {
        if (natsBuilder is null) throw new ArgumentNullException(nameof(natsBuilder));
        if (natsQueue is null) throw new ArgumentNullException(nameof(natsQueue));
        if (natsBuilderConfig is null) throw new ArgumentNullException(nameof(natsBuilderConfig));

        var b = natsBuilder.ReplyToQueue(natsQueue);
        natsBuilderConfig(b);
        return b;
    }
}