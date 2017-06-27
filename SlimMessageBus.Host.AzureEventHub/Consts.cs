using System;

namespace SlimMessageBus.Host.AzureEventHub
{
    public class Consts
    {
        public const string CheckpointCount = "CheckpointCount";
        public const string CheckpointDuration = "CheckpointDuration";

        public static readonly int CheckpointCountDefault = 20;
        public static readonly int CheckpointDurationDefault = (int) TimeSpan.FromSeconds(5).TotalMilliseconds;
    }
}