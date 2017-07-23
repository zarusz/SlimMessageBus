using System;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host
{
    public static class CheckpointSettingsExtensions
    {
        /// <summary>
        /// Checkpoint every N-th processed message.
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="numberOfMessages"></param>
        /// <returns></returns>
        public static ConsumerSettings CheckpointEvery(this ConsumerSettings settings, int numberOfMessages)
        {
            settings.Properties[CheckpointSettings.CheckpointCount] = numberOfMessages;
            return settings;
        }

        /// <summary>
        /// Checkpoint after T elapsed time.
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="duration"></param>
        /// <returns></returns>
        public static ConsumerSettings CheckpointAfter(this ConsumerSettings settings, TimeSpan duration)
        {
            settings.Properties[CheckpointSettings.CheckpointDuration] = duration;
            return settings;
        }

        /// <summary>
        /// Checkpoint every N-th processed message.
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="numberOfMessages"></param>
        /// <returns></returns>
        public static RequestResponseSettings CheckpointEvery(this RequestResponseSettings settings, int numberOfMessages)
        {
            settings.Properties[CheckpointSettings.CheckpointCount] = numberOfMessages;
            return settings;
        }

        /// <summary>
        /// Checkpoint after T elapsed time.
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="duration"></param>
        /// <returns></returns>
        public static RequestResponseSettings CheckpointAfter(this RequestResponseSettings settings, TimeSpan duration)
        {
            settings.Properties[CheckpointSettings.CheckpointDuration] = duration;
            return settings;
        }
    }
}