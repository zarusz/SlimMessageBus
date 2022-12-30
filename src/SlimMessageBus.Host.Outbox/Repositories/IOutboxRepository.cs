namespace SlimMessageBus.Host.Outbox;

public interface IOutboxRepository
{
    /// <summary>
    /// Initializes the data schema for the outbox. Invoked once on bus start.
    /// </summary>
    /// <returns></returns>
    Task Initialize(CancellationToken token);
    Task Save(OutboxMessage message, CancellationToken token);
    Task<int> TryToLock(string instanceId, DateTime expiresOn, CancellationToken token);
    Task<IReadOnlyList<OutboxMessage>> FindNextToSend(int top, string instanceId, CancellationToken token);
    Task UpdateToSent(IReadOnlyCollection<Guid> ids, CancellationToken token);
    Task DeleteSent(DateTime olderThan, CancellationToken token);
}
