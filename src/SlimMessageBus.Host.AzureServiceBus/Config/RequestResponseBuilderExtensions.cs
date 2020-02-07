using System;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureServiceBus
{
    public static class RequestResponseBuilderExtensions
    {
        public static RequestResponseBuilder ReplyToQueue(this RequestResponseBuilder builder, string queue)
        {
            if (builder is null) throw new ArgumentNullException(nameof(builder));
            if (queue is null) throw new ArgumentNullException(nameof(queue));

            builder.Settings.Topic = queue;
            builder.Settings.SetKind(PathKind.Queue);
            return builder;
        }

        public static RequestResponseBuilder ReplyToQueue(this RequestResponseBuilder builder, string queue, Action<RequestResponseBuilder> builderConfig)
        {
            if (builder is null) throw new ArgumentNullException(nameof(builder));
            if (queue is null) throw new ArgumentNullException(nameof(queue));
            if (builderConfig is null) throw new ArgumentNullException(nameof(builderConfig));

            var b = builder.ReplyToQueue(queue);
            builderConfig(b);
            return b;
        }
    }
}