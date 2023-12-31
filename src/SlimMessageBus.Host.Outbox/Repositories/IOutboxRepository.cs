namespace SlimMessageBus.Host.Outbox;

public interface IOutboxRepository
{
    Task Save(OutboxMessage message, CancellationToken token);
    Task<int> TryToLock(string instanceId, DateTime expiresOn, CancellationToken token);
    Task<IReadOnlyList<OutboxMessage>> FindNextToSend(string instanceId, CancellationToken token);
    Task UpdateToSent(IReadOnlyCollection<Guid> ids, CancellationToken token);
    Task DeleteSent(DateTime olderThan, CancellationToken token);
}
