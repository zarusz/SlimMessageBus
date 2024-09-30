namespace SlimMessageBus.Host.Outbox;

public interface IOutboxRepository
{
    OutboxMessage Create();
    Task Save(OutboxMessage message, CancellationToken token);
    Task<IReadOnlyCollection<OutboxMessage>> LockAndSelect(string instanceId, int batchSize, bool tableLock, TimeSpan lockDuration, CancellationToken token);
    Task AbortDelivery(IReadOnlyCollection<OutboxMessage> messages, CancellationToken token);
    Task UpdateToSent(IReadOnlyCollection<OutboxMessage> messages, CancellationToken token);
    Task IncrementDeliveryAttempt(IReadOnlyCollection<OutboxMessage> messages, int maxDeliveryAttempts, CancellationToken token);
    Task DeleteSent(DateTime olderThan, CancellationToken token);
    Task<bool> RenewLock(string instanceId, TimeSpan lockDuration, CancellationToken token);
}
