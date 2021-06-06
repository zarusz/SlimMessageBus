namespace SlimMessageBus.Host.Redis
{
    using SlimMessageBus.Host.Config;
    using System;

    public static class ConsumerBuilderExtensions
    {
        public static TopicConsumerBuilder<T> Queue<T>(this ConsumerBuilder<T> builder, string queue)
        {
            if (builder is null) throw new ArgumentNullException(nameof(builder));

            var b = new TopicConsumerBuilder<T>(queue, builder.MessageType, builder.Settings);
            b.ConsumerSettings.PathKind = PathKind.Queue;
            return b;
        }

        public static TopicConsumerBuilder<T> Queue<T>(this ConsumerBuilder<T> builder, string queue, Action<TopicConsumerBuilder<T>> topicConfig)
        {
            if (builder is null) throw new ArgumentNullException(nameof(builder));
            if (topicConfig is null) throw new ArgumentNullException(nameof(topicConfig));

            var b = builder.Queue(queue);
            topicConfig(b);
            return b;
        }
    }
}