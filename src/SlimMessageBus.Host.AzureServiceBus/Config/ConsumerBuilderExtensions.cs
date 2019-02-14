using System;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureServiceBus
{
    public static class ConsumerBuilderExtensions
    {
        public static TopicConsumerBuilder<T> Queue<T>(this ConsumerBuilder<T> builder, string queue)
        {
            var b = new TopicConsumerBuilder<T>(queue, builder.MessageType, builder.Settings);
            b.ConsumerSettings.SetKind(PathKind.Queue);
            return b;
        }

        public static TopicConsumerBuilder<T> Queue<T>(this ConsumerBuilder<T> builder, string queue, Action<TopicConsumerBuilder<T>> topicConfig)
        {
            var b = builder.Queue(queue);
            topicConfig(b);
            return b;
        }
    }
}