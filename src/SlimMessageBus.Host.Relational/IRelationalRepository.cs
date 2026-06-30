namespace SlimMessageBus.Host.Relational;

public interface IRelationalRepository<TMessage>
    where TMessage : IRelationalTransportMessage
{
    Task UpsertSubscription(string path, string subscriptionName, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<string>> GetSubscriptions(string path, CancellationToken cancellationToken);
    Task Insert(TMessage message, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<TMessage>> LockAndSelect(string path, PathKind pathKind, string subscriptionName, string instanceId, int batchSize, TimeSpan lockDuration, CancellationToken cancellationToken);
    Task Complete(IReadOnlyCollection<TMessage> messages, CancellationToken cancellationToken);
    Task Fail(IReadOnlyCollection<TMessage> messages, int maxDeliveryAttempts, CancellationToken cancellationToken);
}
