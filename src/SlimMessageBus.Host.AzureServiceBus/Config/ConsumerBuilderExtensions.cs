using System;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureServiceBus
{
    public static class ConsumerBuilderExtensions
    {
        public static TopicSubscriberBuilder<T> Queue<T>(this ConsumerBuilder<T> builder, string queue)
        {
            var b = new TopicSubscriberBuilder<T>(queue, builder.MessageType, builder.Settings);
            b.ConsumerSettings.SetKind(PathKind.Queue);
            return b;
        }

        public static TopicSubscriberBuilder<T> Queue<T>(this ConsumerBuilder<T> builder, string queue, Action<TopicSubscriberBuilder<T>> topicConfig)
        {
            var b = builder.Queue(queue);
            topicConfig(b);
            return b;
        }
    }
}