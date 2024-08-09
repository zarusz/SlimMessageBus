namespace SlimMessageBus.Host.Outbox;

using System.Transactions;

public abstract class TransactionScopeConsumerInterceptor
{
}

/// <summary>
/// Wraps the consumer in an <see cref="TransactionScope"/> (conditionally).
/// </summary>
/// <typeparam name="T"></typeparam>
public class TransactionScopeConsumerInterceptor<T>(ILogger<TransactionScopeConsumerInterceptor> logger, OutboxSettings settings)
    : TransactionScopeConsumerInterceptor, IConsumerInterceptor<T> where T : class
{
    public async Task<object> OnHandle(T message, Func<Task<object>> next, IConsumerContext context)
    {
        logger.LogTrace("TransactionScope - creating...");
        using var tx = new TransactionScope(
            scopeOption: TransactionScopeOption.Required,
            asyncFlowOption: TransactionScopeAsyncFlowOption.Enabled,
            transactionOptions: new TransactionOptions { IsolationLevel = settings.TransactionScopeIsolationLevel });

        logger.LogDebug("TransactionScope - created");

        var result = await next();

        logger.LogTrace("TransactionScope - completing...");
        tx.Complete();
        logger.LogDebug("TransactionScope - completed");

        return result;
    }
}
