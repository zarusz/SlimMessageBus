namespace SlimMessageBus.Host.Redis
{
    using SlimMessageBus.Host.Config;
    using System;

    public static class ConsumerBuilderExtensions
    {
        public static ConsumerBuilder<T> Queue<T>(this ConsumerBuilder<T> builder, string queue)
        {
            if (builder is null) throw new ArgumentNullException(nameof(builder));

            builder.Path(queue);
            builder.ConsumerSettings.PathKind = PathKind.Queue;
            return builder;
        }

        public static ConsumerBuilder<T> Queue<T>(this ConsumerBuilder<T> builder, string queue, Action<ConsumerBuilder<T>> topicConfig)
        {
            if (builder is null) throw new ArgumentNullException(nameof(builder));
            if (topicConfig is null) throw new ArgumentNullException(nameof(topicConfig));

            var b = builder.Queue(queue);
            topicConfig(b);
            return b;
        }
    }
}