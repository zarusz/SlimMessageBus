namespace SlimMessageBus.Host.Outbox;

public interface IOutboxMessageRepository : IOutboxMessageFactory
{
    Task<IReadOnlyCollection<OutboxMessage>> LockAndSelect(string instanceId, int batchSize, bool tableLock, TimeSpan lockDuration, CancellationToken cancellationToken);
    Task AbortDelivery(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
    Task UpdateToSent(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
    Task IncrementDeliveryAttempt(IReadOnlyCollection<Guid> ids, int maxDeliveryAttempts, CancellationToken cancellationToken);
    Task DeleteSent(DateTime olderThan, CancellationToken cancellationToken);
    Task<bool> RenewLock(string instanceId, TimeSpan lockDuration, CancellationToken cancellationToken);
}
