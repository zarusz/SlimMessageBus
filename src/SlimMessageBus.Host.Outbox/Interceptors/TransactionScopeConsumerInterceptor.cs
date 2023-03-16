namespace SlimMessageBus.Host.Outbox;

using System.Transactions;

public abstract class TransactionScopeConsumerInterceptor
{
}

/// <summary>
/// Wraps the consumer in an <see cref="TransactionScope"/> (conditionally).
/// </summary>
/// <typeparam name="T"></typeparam>
public class TransactionScopeConsumerInterceptor<T> : TransactionScopeConsumerInterceptor, IConsumerInterceptor<T> where T : class
{
    private readonly ILogger _logger;
    private readonly OutboxSettings _settings;

    public TransactionScopeConsumerInterceptor(ILogger<TransactionScopeConsumerInterceptor> logger, OutboxSettings settings)
    {
        _logger = logger;
        _settings = settings;
    }

    public async Task<object> OnHandle(T message, Func<Task<object>> next, IConsumerContext context)
    {
        _logger.LogTrace("TransactionScope - creating...");
        using var tx = new TransactionScope(scopeOption: TransactionScopeOption.Required, asyncFlowOption: TransactionScopeAsyncFlowOption.Enabled, transactionOptions: new TransactionOptions { IsolationLevel = _settings.TransactionScopeIsolationLevel });
        _logger.LogDebug("TransactionScope - created");

        var result = await next();

        _logger.LogTrace("TransactionScope - completing...");
        tx.Complete();
        _logger.LogDebug("TransactionScope - completed");

        return result;
    }
}
