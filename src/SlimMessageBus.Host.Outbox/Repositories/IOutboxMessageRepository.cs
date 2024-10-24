namespace SlimMessageBus.Host.Outbox;

public interface IOutboxMessageRepository<TOutboxMessage, TOutboxMessageKey>
    where TOutboxMessage : class
{
    Task<IReadOnlyCollection<TOutboxMessage>> LockAndSelect(string instanceId, int batchSize, bool tableLock, TimeSpan lockDuration, CancellationToken cancellationToken);
    Task AbortDelivery(IReadOnlyCollection<TOutboxMessageKey> ids, CancellationToken cancellationToken);
    Task UpdateToSent(IReadOnlyCollection<TOutboxMessageKey> ids, CancellationToken cancellationToken);
    Task IncrementDeliveryAttempt(IReadOnlyCollection<TOutboxMessageKey> ids, int maxDeliveryAttempts, CancellationToken cancellationToken);
    Task DeleteSent(DateTime olderThan, CancellationToken cancellationToken);
    Task<bool> RenewLock(string instanceId, TimeSpan lockDuration, CancellationToken cancellationToken);
}

