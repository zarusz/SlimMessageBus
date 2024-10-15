namespace SlimMessageBus.Host.Outbox.DbContext;

public class DbContextOutboxMessageRepository(Microsoft.EntityFrameworkCore.DbContext dbContext) : IOutboxMessageRepository, IHasDbContext
{
    public Microsoft.EntityFrameworkCore.DbContext DbContext { get; } = dbContext;

    protected readonly DbSet<OutboxMessage> _outboxMessages = dbContext.Set<OutboxMessage>();

    public Task<Guid> Create(string busName, IDictionary<string, object> headers, string path, string messageType, byte[] messagePayload, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task AbortDelivery(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task DeleteSent(DateTime olderThan, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task IncrementDeliveryAttempt(IReadOnlyCollection<Guid> ids, int maxDeliveryAttempts, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyCollection<OutboxMessage>> LockAndSelect(string instanceId, int batchSize, bool tableLock, TimeSpan lockDuration, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<bool> RenewLock(string instanceId, TimeSpan lockDuration, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task UpdateToSent(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}