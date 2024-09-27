namespace SlimMessageBus.Host.Outbox;

public interface IOutboxRepository<TOutboxKey>
{
    Task<TOutboxKey> GenerateId(CancellationToken cancellationToken);
    Task Save(OutboxMessage<TOutboxKey> message, CancellationToken token);
    Task<IReadOnlyCollection<OutboxMessage<TOutboxKey>>> LockAndSelect(string instanceId, int batchSize, bool tableLock, TimeSpan lockDuration, CancellationToken token);
    Task AbortDelivery(IReadOnlyCollection<TOutboxKey> ids, CancellationToken token);
    Task UpdateToSent(IReadOnlyCollection<TOutboxKey> ids, CancellationToken token);
    Task IncrementDeliveryAttempt(IReadOnlyCollection<TOutboxKey> ids, int maxDeliveryAttempts, CancellationToken token);
    Task DeleteSent(DateTime olderThan, CancellationToken token);
    Task<bool> RenewLock(string instanceId, TimeSpan lockDuration, CancellationToken token);
}
