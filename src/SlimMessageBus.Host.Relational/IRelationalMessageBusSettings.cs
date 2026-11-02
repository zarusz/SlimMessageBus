namespace SlimMessageBus.Host.Relational;

public interface IRelationalMessageBusSettings
{
    TimeSpan PollDelay { get; }
    /// <summary>
    /// Duration of immediate polling at startup and after messages are dequeued, before applying <see cref="PollDelay"/> to empty polls.
    /// Set to <see cref="TimeSpan.Zero"/> to delay after every empty poll.
    /// </summary>
    TimeSpan PollImmediateDuration { get; }
    TimeSpan LockDuration { get; }
    int PollBatchSize { get; }
    int MaxDeliveryAttempts { get; }
}
