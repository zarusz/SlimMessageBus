namespace SlimMessageBus.Host;

using System.Diagnostics;

public class CheckpointTrigger : ICheckpointTrigger
{
    private readonly ILogger<CheckpointTrigger> _logger;

    private readonly int _checkpointCount;
    private readonly int _checkpointDuration;

    private int _lastCheckpointCount;
    private readonly Stopwatch _lastCheckpointDuration;

    public CheckpointTrigger(int countLimit, TimeSpan durationlimit, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<CheckpointTrigger>();

        _checkpointCount = countLimit;
        _checkpointDuration = (int)durationlimit.TotalMilliseconds;

        _lastCheckpointCount = 0;
        _lastCheckpointDuration = new Stopwatch();
    }

    public CheckpointTrigger(HasProviderExtensions settings, ILoggerFactory loggerFactory)
        : this(settings.GetOrDefault(CheckpointSettings.CheckpointCount, CheckpointSettings.CheckpointCountDefault),
               settings.GetOrDefault(CheckpointSettings.CheckpointDuration, CheckpointSettings.CheckpointDurationDefault),
               loggerFactory)
    {
    }

    public static bool IsConfigured(HasProviderExtensions settings)
        => settings.GetOrDefault<int?>(CheckpointSettings.CheckpointCount, null) != null || settings.GetOrDefault<TimeSpan?>(CheckpointSettings.CheckpointDuration, null) != null;

    public static (int CheckpontCount, TimeSpan CheckpointDuration) GetConfiguration(HasProviderExtensions settings)
        => (settings.GetOrDefault(CheckpointSettings.CheckpointCount, CheckpointSettings.CheckpointCountDefault),
            settings.GetOrDefault(CheckpointSettings.CheckpointDuration, CheckpointSettings.CheckpointDurationDefault));


    #region Implementation of ICheckpointTrigger

    public bool IsEnabled
        => _lastCheckpointCount >= _checkpointCount || (_lastCheckpointCount > 0 && _lastCheckpointDuration.ElapsedMilliseconds > _checkpointDuration);

    public bool Increment()
    {
        if (_lastCheckpointCount == 0)
        {
            // Note: Start the timer only when first message arrives
            _lastCheckpointDuration.Restart();
        }
        _lastCheckpointCount++;

        var enabled = IsEnabled;
        if (enabled && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Checkpoint triggered after Count: {CheckpointCount}, Duration: {CheckpointDuration} (s)", _lastCheckpointCount, _lastCheckpointDuration.Elapsed.Seconds);
        }

        return enabled;
    }

    public void Reset()
    {
        _lastCheckpointCount = 0;
        _lastCheckpointDuration.Restart();
    }

    #endregion
}
