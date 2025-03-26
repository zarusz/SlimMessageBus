namespace SlimMessageBus.Host.Outbox;

public interface IOutboxMessageRepository<TOutboxMessage>
    where TOutboxMessage : class
{
    Task<IReadOnlyCollection<TOutboxMessage>> LockAndSelect(string instanceId, int batchSize, bool tableLock, TimeSpan lockDuration, CancellationToken cancellationToken);
    Task AbortDelivery(IReadOnlyCollection<TOutboxMessage> messages, CancellationToken cancellationToken);
    Task UpdateToSent(IReadOnlyCollection<TOutboxMessage> messages, CancellationToken cancellationToken);
    Task IncrementDeliveryAttempt(IReadOnlyCollection<TOutboxMessage> messages, int maxDeliveryAttempts, CancellationToken cancellationToken);
    Task<int> DeleteSent(DateTimeOffset olderThan, int batchSize, CancellationToken cancellationToken);
    Task<bool> RenewLock(string instanceId, TimeSpan lockDuration, CancellationToken cancellationToken);
}