namespace SlimMessageBus.Host.Outbox.MongoDb.Interceptors;

public interface IMongoDbTransactionConsumerInterceptor { }

/// <summary>
/// Wraps the consumer in a MongoDB session transaction (requires a MongoDB replica set).
/// </summary>
/// <typeparam name="T">The consumed message type.</typeparam>
public class MongoDbTransactionConsumerInterceptor<T>(
    ILogger<IMongoDbTransactionConsumerInterceptor> logger,
    IMongoDbTransactionService transactionService)
    : IMongoDbTransactionConsumerInterceptor, IConsumerInterceptor<T> where T : class
{
    public async Task<object> OnHandle(T message, Func<Task<object>> next, IConsumerContext context)
    {
        logger.LogDebug("MongoDbTransaction - begin");
        await transactionService.BeginTransaction();
        object result;
        try
        {
            result = await next();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "MongoDbTransaction - rollback");
            await transactionService.RollbackTransaction();
            throw;
        }
        logger.LogDebug("MongoDbTransaction - commit");
        await transactionService.CommitTransaction();
        return result;
    }
}
