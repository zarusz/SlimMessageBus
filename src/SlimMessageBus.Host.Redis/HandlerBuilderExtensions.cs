﻿namespace SlimMessageBus.Host.Redis
{
    using SlimMessageBus.Host.Config;
    using System;

    public static class HandlerBuilderExtensions
    {
        /// <summary>
        /// Configure queue name that incoming requests (<see cref="TRequest"/>) are expected on.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="queue">Queue name</param>
        /// <returns></returns>
        public static TopicHandlerBuilder<TRequest, TResponse> Queue<TRequest, TResponse>(this HandlerBuilder<TRequest, TResponse> builder, string queue)
        {
            if (builder is null) throw new ArgumentNullException(nameof(builder));

            var b = new TopicHandlerBuilder<TRequest, TResponse>(queue, builder.Settings);
            b.ConsumerSettings.PathKind = PathKind.Queue;
            return b;
        }

        /// <summary>
        /// Configure queue name that incoming requests (<see cref="TRequest"/>) are expected on.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="queue">Queue name</param>
        /// <param name="queueConfig"></param>
        /// <returns></returns>
        public static TopicHandlerBuilder<TRequest, TResponse> Topic<TRequest, TResponse>(this HandlerBuilder<TRequest, TResponse> builder, string queue, Action<TopicHandlerBuilder<TRequest, TResponse>> queueConfig)
        {
            if (builder is null) throw new ArgumentNullException(nameof(builder));
            if (queueConfig is null) throw new ArgumentNullException(nameof(queueConfig));

            var b = builder.Queue(queue);
            queueConfig(b);
            return b;
        }
    }
}