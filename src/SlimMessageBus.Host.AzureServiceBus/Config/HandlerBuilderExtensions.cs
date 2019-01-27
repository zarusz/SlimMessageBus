using System;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureServiceBus.Config
{
    public static class HandlerBuilderExtensions
    {
        /// <summary>
        /// Configure queue name that incoming requests (<see cref="TRequest"/>) are expected on.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="queue">Queue name</param>
        /// <returns></returns>
        public static TopicHandlerBuilder<TRequest, TResponse> Queue<TRequest, TResponse>(this HandlerBuilder<TRequest, TResponse> builder, string queue)
            where TRequest : IRequestMessage<TResponse>
        {
            var b = new TopicHandlerBuilder<TRequest, TResponse>(queue, builder.Settings);
            b.ConsumerSettings.SetKind(PathKind.Queue);
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
            where TRequest : IRequestMessage<TResponse>
        {
            var b = builder.Queue(queue);
            queueConfig(b);
            return b;
        }
    }
}