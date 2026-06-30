namespace SlimMessageBus.Host.PostgreSql;

public interface IPostgreSqlRepository
{
    NpgsqlConnection Connection { get; }
    Task EnsureConnection(CancellationToken cancellationToken);
    Task UpsertSubscription(string path, string subscriptionName, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<string>> GetSubscriptions(string path, CancellationToken cancellationToken);
    Task Insert(PostgreSqlTransportMessage message, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<PostgreSqlTransportMessage>> LockAndSelect(string path, PathKind pathKind, string? subscriptionName, string instanceId, int batchSize, TimeSpan lockDuration, CancellationToken cancellationToken);
    Task Complete(IReadOnlyCollection<PostgreSqlTransportMessage> messages, CancellationToken cancellationToken);
    Task Fail(IReadOnlyCollection<PostgreSqlTransportMessage> messages, int maxDeliveryAttempts, CancellationToken cancellationToken);
}
