namespace SlimMessageBus.Host.Sql;

public interface ISqlRepository
{
    Task EnsureConnection();
    Task UpsertSubscription(string path, string subscriptionName, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<string>> GetSubscriptions(string path, CancellationToken cancellationToken);
    Task Insert(SqlTransportMessage message, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SqlTransportMessage>> LockAndSelect(string path, PathKind pathKind, string subscriptionName, string instanceId, int batchSize, TimeSpan lockDuration, CancellationToken cancellationToken);
    Task Complete(IReadOnlyCollection<SqlTransportMessage> messages, CancellationToken cancellationToken);
    Task Fail(IReadOnlyCollection<SqlTransportMessage> messages, int maxDeliveryAttempts, CancellationToken cancellationToken);
}
