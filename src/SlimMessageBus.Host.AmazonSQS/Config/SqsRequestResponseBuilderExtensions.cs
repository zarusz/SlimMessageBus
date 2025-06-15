namespace SlimMessageBus.Host.AmazonSQS;

public static class SqsRequestResponseBuilderExtensions
{
    public static RequestResponseBuilder ReplyToQueue(this RequestResponseBuilder builder, string queue, Action<RequestResponseBuilder> builderConfig = null)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (queue is null) throw new ArgumentNullException(nameof(queue));

        builder.Settings.Path = queue;
        builder.Settings.PathKind = PathKind.Queue;
        SqsProperties.UnderlyingQueue.Set(builder.Settings, queue);

        builderConfig?.Invoke(builder);

        return builder;
    }
}