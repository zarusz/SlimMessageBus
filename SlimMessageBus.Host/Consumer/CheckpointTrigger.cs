using System;
using System.Diagnostics;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host
{
    public class CheckpointTrigger : ICheckpointTrigger
    {
        private readonly int _checkpointCount;
        private readonly int _checkpointDuration;

        private int _lastCheckpointCount;
        private readonly Stopwatch _lastCheckpointDuration = new Stopwatch();

        public CheckpointTrigger(int countLimit, TimeSpan durationlimit)
        {
            _checkpointCount = countLimit;
            _checkpointDuration = (int) durationlimit.TotalMilliseconds;

            _lastCheckpointCount = 0;
            _lastCheckpointDuration.Start();
        }

        public CheckpointTrigger(HasProviderExtensions settings)
            : this(settings.GetOrDefault(CheckpointSettings.CheckpointCount, CheckpointSettings.CheckpointCountDefault), 
                   settings.GetOrDefault(CheckpointSettings.CheckpointDuration, CheckpointSettings.CheckpointDurationDefault))
        {
        }

        #region Implementation of ICheckpointTrigger

        public bool IsEnabled
            =>
                _lastCheckpointCount >= _checkpointCount ||
                _lastCheckpointDuration.ElapsedMilliseconds > _checkpointDuration;

        public bool Increment()
        {
            _lastCheckpointCount++;
            return IsEnabled;
        }

        public void Reset()
        {
            _lastCheckpointCount = 0;
            _lastCheckpointDuration.Restart();
        }

        #endregion
    }
}
