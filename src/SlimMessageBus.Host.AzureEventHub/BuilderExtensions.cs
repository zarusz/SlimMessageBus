using SlimMessageBus.Host.Config;
using System;

namespace SlimMessageBus.Host.AzureEventHub
{
    public static class BuilderExtensions
    {
        /// <summary>
        /// Set Azure EventHub subscriber name.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="builder"></param>
        /// <param name="group"></param>
        /// <returns></returns>
        public static T Group<T>(this T builder, string group)
            where T : AbstractTopicConsumerBuilder
        {
            builder.ConsumerSettings.SetGroup(group);
            return builder;
        }

        /// <summary>
        /// Set Azure EventHub subscriber name.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="group"></param>
        /// <returns></returns>
        public static RequestResponseBuilder Group(this RequestResponseBuilder builder, string group)
        {
            builder.Settings.SetGroup(group);
            return builder;
        }

        /// <summary>
        /// Checkpoint every N-th processed message.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="numberOfMessages"></param>
        /// <returns></returns>
        public static T CheckpointEvery<T>(this T builder, int numberOfMessages)
            where T : AbstractTopicConsumerBuilder
        {
            builder.ConsumerSettings.Properties[CheckpointSettings.CheckpointCount] = numberOfMessages;
            return builder;
        }

        /// <summary>
        /// Checkpoint after T elapsed time.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="duration"></param>
        /// <returns></returns>
        public static T CheckpointAfter<T>(this T builder, TimeSpan duration)
            where T : AbstractTopicConsumerBuilder
        {
            builder.ConsumerSettings.Properties[CheckpointSettings.CheckpointDuration] = duration;
            return builder;
        }

        /// <summary>
        /// Checkpoint every N-th processed message.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="numberOfMessages"></param>
        /// <returns></returns>
        public static RequestResponseBuilder CheckpointEvery(this RequestResponseBuilder builder, int numberOfMessages)
        {
            builder.Settings.Properties[CheckpointSettings.CheckpointCount] = numberOfMessages;
            return builder;
        }

        /// <summary>
        /// Checkpoint after T elapsed time.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="duration"></param>
        /// <returns></returns>
        public static RequestResponseBuilder CheckpointAfter(this RequestResponseBuilder builder, TimeSpan duration)
        {
            builder.Settings.Properties[CheckpointSettings.CheckpointDuration] = duration;
            return builder;
        }
    }
}
