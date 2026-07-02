namespace SlimMessageBus.Host.Relational;

public interface IRelationalMessageBusSettings
{
    TimeSpan PollDelay { get; }
    TimeSpan LockDuration { get; }
    int PollBatchSize { get; }
    int MaxDeliveryAttempts { get; }
}
