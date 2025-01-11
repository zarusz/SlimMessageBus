namespace SlimMessageBus.Host;

using System.Diagnostics;

public partial class CheckpointTrigger : ICheckpointTrigger
{
    private readonly ILogger<CheckpointTrigger> _logger;

    private readonly int _checkpointCount;
    private readonly int _checkpointDuration;

    private int _lastCheckpointCount;
    private readonly Stopwatch _lastCheckpointDuration;

    public CheckpointTrigger(CheckpointValue checkpointValue, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<CheckpointTrigger>();

        _checkpointCount = checkpointValue.CheckpointCount;
        _checkpointDuration = (int)checkpointValue.CheckpointDuration.TotalMilliseconds;

        _lastCheckpointCount = 0;
        _lastCheckpointDuration = new Stopwatch();
    }

    public CheckpointTrigger(HasProviderExtensions settings, ILoggerFactory loggerFactory)
        : this(GetCheckpointValue(settings), loggerFactory)
    {
    }

    public static bool IsConfigured(HasProviderExtensions settings)
        => settings.GetOrDefault<int?>(CheckpointSettings.CheckpointCount, null) != null || settings.GetOrDefault<TimeSpan?>(CheckpointSettings.CheckpointDuration, null) != null;

    public static CheckpointValue GetCheckpointValue(HasProviderExtensions settings)
        => new(settings.GetOrDefault(CheckpointSettings.CheckpointCount, CheckpointSettings.CheckpointCountDefault),
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
            LogCheckpointTriggered(_lastCheckpointCount, _lastCheckpointDuration.Elapsed.Seconds);
        }

        return enabled;
    }

    public void Reset()
    {
        _lastCheckpointCount = 0;
        _lastCheckpointDuration.Restart();
    }

    #endregion

    #region Logging

    [LoggerMessage(
       EventId = 0,
       Level = LogLevel.Debug,
       Message = "Checkpoint triggered after Count: {CheckpointCount}, Duration: {CheckpointDuration} (s)")]
    private partial void LogCheckpointTriggered(int checkpointCount, int checkpointDuration);

    #endregion
}

#if NETSTANDARD2_0

public partial class CheckpointTrigger
{
    private partial void LogCheckpointTriggered(int checkpointCount, int checkpointDuration)
        => _logger.LogDebug("Checkpoint triggered after Count: {CheckpointCount}, Duration: {CheckpointDuration} (s)", checkpointCount, checkpointDuration);
}

#endif