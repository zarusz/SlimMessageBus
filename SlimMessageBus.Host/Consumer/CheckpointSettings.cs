using System;

namespace SlimMessageBus.Host
{
    public class CheckpointSettings
    {
        public const string CheckpointCount = "CheckpointCount";
        public const string CheckpointDuration = "CheckpointDuration";

        public static readonly int CheckpointCountDefault = 20;
        public static readonly TimeSpan CheckpointDurationDefault = TimeSpan.FromSeconds(5);
    }
}