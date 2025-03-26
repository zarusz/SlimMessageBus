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

    private void LogTime(string name, Stopwatch sw)
        => logger.LogInformation("Method {MethodName} took {Elapsed}", name, sw.Elapsed);

    public Task AbortDelivery(IReadOnlyCollection<SqlOutboxMessage> messages, CancellationToken cancellationToken)
        => MeasureMethod(nameof(AbortDelivery), () => target.AbortDelivery(messages, cancellationToken));

    public Task<OutboxMessage> Create(string busName, IDictionary<string, object> headers, string path, string messageType, byte[] messagePayload, CancellationToken cancellationToken)
        => MeasureMethod(nameof(Create), () => target.Create(busName, headers, path, messageType, messagePayload, cancellationToken));

    public Task<int> DeleteSent(DateTimeOffset olderThan, int batchSize, CancellationToken cancellationToken)
        => MeasureMethod(nameof(DeleteSent), () => target.DeleteSent(olderThan, batchSize, cancellationToken));

    public Task IncrementDeliveryAttempt(IReadOnlyCollection<SqlOutboxMessage> messages, int maxDeliveryAttempts, CancellationToken cancellationToken)
        => MeasureMethod(nameof(IncrementDeliveryAttempt), () => target.IncrementDeliveryAttempt(messages, maxDeliveryAttempts, cancellationToken));

    public Task<IReadOnlyCollection<SqlOutboxMessage>> LockAndSelect(string instanceId, int batchSize, bool tableLock, TimeSpan lockDuration, CancellationToken cancellationToken)
        => MeasureMethod(nameof(LockAndSelect), () => target.LockAndSelect(instanceId, batchSize, tableLock, lockDuration, cancellationToken));

    public Task<bool> RenewLock(string instanceId, TimeSpan lockDuration, CancellationToken cancellationToken)
        => MeasureMethod(nameof(RenewLock), () => target.RenewLock(instanceId, lockDuration, cancellationToken));

    public Task UpdateToSent(IReadOnlyCollection<SqlOutboxMessage> messages, CancellationToken cancellationToken)
        => MeasureMethod(nameof(UpdateToSent), () => target.UpdateToSent(messages, cancellationToken));
}
