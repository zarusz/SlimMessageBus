namespace SlimMessageBus.Host.AzureServiceBus
{
    using System;
    using Azure.Messaging.ServiceBus;
    using SlimMessageBus.Host.Config;

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

        /// <summary>
        /// Azure Service Bus consumer setting. See underlying client for more details: https://docs.microsoft.com/en-us/dotnet/api/azure.messaging.servicebus.servicebusprocessoroptions.maxautolockrenewalduration
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="builder"></param>
        /// <param name="duration"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static ConsumerBuilder<T> MaxAutoLockRenewalDuration<T>(this ConsumerBuilder<T> builder, TimeSpan duration)
        {
            if (builder is null) throw new ArgumentNullException(nameof(builder));

            builder.ConsumerSettings.SetMaxAutoLockRenewalDuration(duration);

            return builder;
        }

        /// <summary>
        /// Azure Service Bus consumer setting. See underlying client for more details: https://docs.microsoft.com/en-us/dotnet/api/azure.messaging.servicebus.servicebusprocessoroptions.subqueue
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="builder"></param>
        /// <param name="duration"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static ConsumerBuilder<T> SubQueue<T>(this ConsumerBuilder<T> builder, SubQueue subQueue)
        {
            if (builder is null) throw new ArgumentNullException(nameof(builder));

            builder.ConsumerSettings.SetSubQueue(subQueue);

            return builder;
        }

        /// <summary>
        /// Azure Service Bus consumer setting. See underlying client for more details: https://docs.microsoft.com/en-us/dotnet/api/azure.messaging.servicebus.servicebusprocessoroptions.prefetchcount
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="builder"></param>
        /// <param name="prefetchCount "></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static ConsumerBuilder<T> PrefetchCount<T>(this ConsumerBuilder<T> builder, int prefetchCount)
        {
            if (builder is null) throw new ArgumentNullException(nameof(builder));

            builder.ConsumerSettings.SetPrefetchCount(prefetchCount);

            return builder;
        }

        private const string MaxAutoLockRenewalDurationKey = "Asb_MaxAutoLockRenewalDuration";
        private const string SubQueueKey = "Asb_SubQueue";
        private const string PrefetchCountKey = "Asb_PrefetchCount";

        internal static void SetMaxAutoLockRenewalDuration(this AbstractConsumerSettings consumerSettings, TimeSpan duration)
            => consumerSettings.Properties[MaxAutoLockRenewalDurationKey] = duration;

        internal static TimeSpan? GetMaxAutoLockRenewalDuration(this AbstractConsumerSettings consumerSettings, bool required = true)
            => !consumerSettings.Properties.ContainsKey(MaxAutoLockRenewalDurationKey) && !required
                ? null
                : consumerSettings.Properties[MaxAutoLockRenewalDurationKey] as TimeSpan?;

        internal static void SetSubQueue(this AbstractConsumerSettings consumerSettings, SubQueue subQueue)
            => consumerSettings.Properties[SubQueueKey] = subQueue;

        internal static SubQueue? GetSubQueue(this AbstractConsumerSettings consumerSettings, bool required = true)
            => !consumerSettings.Properties.ContainsKey(SubQueueKey) && !required
                ? null
                : consumerSettings.Properties[SubQueueKey] as SubQueue?;

        internal static void SetPrefetchCount(this AbstractConsumerSettings consumerSettings, int prefetchCount)
            => consumerSettings.Properties[PrefetchCountKey] = prefetchCount;

        internal static int? GetPrefetchCount(this AbstractConsumerSettings consumerSettings, bool required = true)
            => !consumerSettings.Properties.ContainsKey(PrefetchCountKey) && !required
                ? null
                : consumerSettings.Properties[PrefetchCountKey] as int?;
    }
}