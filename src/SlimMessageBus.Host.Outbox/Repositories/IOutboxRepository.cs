namespace SlimMessageBus.Host.Outbox;

public interface IOutboxRepository
{
    Task Save(OutboxMessage message, CancellationToken token);
    Task<IReadOnlyCollection<OutboxMessage>> LockAndSelect(string instanceId, int batchSize, bool tableLock, TimeSpan lockDuration, CancellationToken token);
    Task UpdateToSent(IReadOnlyCollection<Guid> ids, CancellationToken token);
    Task IncrementDeliveryAttempt(IReadOnlyCollection<Guid> ids, int maxDeliveryAttempts, CancellationToken token);
    Task DeleteSent(DateTime olderThan, CancellationToken token);
    Task<bool> RenewLock(string instanceId, TimeSpan lockDuration, CancellationToken token);
}
