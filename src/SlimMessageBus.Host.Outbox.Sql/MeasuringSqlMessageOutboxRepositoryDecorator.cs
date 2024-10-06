namespace SlimMessageBus.Host.Outbox.Sql;

using System.Diagnostics;

public class MeasuringSqlMessageOutboxRepositoryDecorator(
    ISqlMessageOutboxRepository target,
    ILogger<MeasuringSqlMessageOutboxRepositoryDecorator> logger)
    : ISqlMessageOutboxRepository
{
    private async Task<T> MeasureMethod<T>(string name, Func<Task<T>> action)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            return await action();
        }
        finally
        {
            LogTime(name, sw);
        }
    }

    private void LogTime(string name, Stopwatch sw)
        => logger.LogInformation("Method {MethodName} took {Elapsed}", name, sw.Elapsed);

    private async Task MeasureMethod(string name, Func<Task> action)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await action();
        }
        finally
        {
            LogTime(name, sw);
        }
    }

    public Task AbortDelivery(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
        => MeasureMethod(nameof(AbortDelivery), () => target.AbortDelivery(ids, cancellationToken));

    public Task<IHasId> Create(string busName, IDictionary<string, object> headers, string path, string messageType, byte[] messagePayload, CancellationToken cancellationToken)
        => MeasureMethod(nameof(Create), () => target.Create(busName, headers, path, messageType, messagePayload, cancellationToken));

    public Task DeleteSent(DateTime olderThan, CancellationToken cancellationToken)
        => MeasureMethod(nameof(DeleteSent), () => target.DeleteSent(olderThan, cancellationToken));

    public Task IncrementDeliveryAttempt(IReadOnlyCollection<Guid> ids, int maxDeliveryAttempts, CancellationToken cancellationToken)
        => MeasureMethod(nameof(IncrementDeliveryAttempt), () => target.IncrementDeliveryAttempt(ids, maxDeliveryAttempts, cancellationToken));

    public Task<IReadOnlyCollection<SqlOutboxMessage>> LockAndSelect(string instanceId, int batchSize, bool tableLock, TimeSpan lockDuration, CancellationToken cancellationToken)
        => MeasureMethod(nameof(LockAndSelect), () => target.LockAndSelect(instanceId, batchSize, tableLock, lockDuration, cancellationToken));

    public Task<bool> RenewLock(string instanceId, TimeSpan lockDuration, CancellationToken cancellationToken)
        => MeasureMethod(nameof(RenewLock), () => target.RenewLock(instanceId, lockDuration, cancellationToken));

    public Task UpdateToSent(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
        => MeasureMethod(nameof(UpdateToSent), () => target.UpdateToSent(ids, cancellationToken));
}
