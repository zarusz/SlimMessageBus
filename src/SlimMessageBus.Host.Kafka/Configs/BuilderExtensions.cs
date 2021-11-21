namespace SlimMessageBus.Host.Kafka
{
    using SlimMessageBus.Host.Config;
    using System;

    public static class BuilderExtensions
    {
        /// <summary>
        /// Configures the Kafka consumer group.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="group"></param>
        /// <returns></returns>
        [Obsolete("Use KafkaGroup() instead")]
        public static T Group<T>(this T builder, string group) where T : AbstractConsumerBuilder
            => builder.KafkaGroup(group);

        public static T KafkaGroup<T>(this T builder, string group) where T : AbstractConsumerBuilder
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            builder.ConsumerSettings.SetGroup(group);
            return builder;
        }

        /// <summary>
        /// Configures the Kafka consumer group.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="group"></param>
        /// <returns></returns>
        [Obsolete("Use KafkaGroup() instead")]
        public static RequestResponseBuilder Group(this RequestResponseBuilder builder, string group)
            => builder.KafkaGroup(group);

        /// <summary>
        /// Configures the Kafka consumer group.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="group"></param>
        /// <returns></returns>
        public static RequestResponseBuilder KafkaGroup(this RequestResponseBuilder builder, string group)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

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
            where T : AbstractConsumerBuilder
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

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
            where T : AbstractConsumerBuilder
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

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
            if (builder == null) throw new ArgumentNullException(nameof(builder));

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
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            builder.Settings.Properties[CheckpointSettings.CheckpointDuration] = duration;
            return builder;
        }
    }
}
