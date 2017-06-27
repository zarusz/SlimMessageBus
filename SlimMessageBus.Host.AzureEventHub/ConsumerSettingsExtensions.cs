using System;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureEventHub
{
    public static class ConsumerSettingsExtensions
    {
        /// <summary>
        /// Checkpoint every N-th processed message.
        /// </summary>
        /// <param name="consumerSettings"></param>
        /// <param name="numberOfMessages"></param>
        /// <returns></returns>
        public static ConsumerSettings CheckpointEvery(this ConsumerSettings consumerSettings, int numberOfMessages)
        {
            consumerSettings.Properties[Consts.CheckpointCount] = numberOfMessages;
            return consumerSettings;
        }
        /// <summary>
        /// Checkpoint after T elapsed time.
        /// </summary>
        /// <param name="consumerSettings"></param>
        /// <param name="duration"></param>
        /// <returns></returns>
        public static ConsumerSettings CheckpointAfter(this ConsumerSettings consumerSettings, TimeSpan duration)
        {
            consumerSettings.Properties[Consts.CheckpointDuration] = (int) duration.TotalMilliseconds;
            return consumerSettings;
        }
    }
}