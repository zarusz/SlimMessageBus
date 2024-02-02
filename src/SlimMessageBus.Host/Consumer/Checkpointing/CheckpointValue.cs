namespace SlimMessageBus.Host;

public record CheckpointValue(int CheckpointCount, TimeSpan CheckpointDuration);
