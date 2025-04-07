namespace SlimMessageBus.Host.Outbox.PostgreSql;

public interface IPostgreSqlTransactionConsumerInterceptor { }

/// <summary>s
/// Wraps the consumer in an <see cref="SqlTransaction"/> (conditionally).
/// </summary>
/// <typeparam name="T"></typeparam>
public class PostgreSqlTransactionConsumerInterceptor<T>(ILogger<IPostgreSqlTransactionConsumerInterceptor> logger, IPostgreSqlTransactionService transactionService)
    : IPostgreSqlTransactionConsumerInterceptor, IConsumerInterceptor<T> where T : class
{
    public async Task<object> OnHandle(T message, Func<Task<object>> next, IConsumerContext context)
    {
        logger.LogTrace("SqlTransaction - creating...");
        await transactionService.BeginTransaction();
        try
        {
            logger.LogDebug("SqlTransaction - created");

            var result = await next();

            logger.LogTrace("SqlTransaction - committing...");
            await transactionService.CommitTransaction();
            logger.LogDebug("SqlTransaction - committed");
            return result;
        }
        catch
        {
            logger.LogTrace("SqlTransaction - rolling back...");
            await transactionService.RollbackTransaction();
            logger.LogDebug("SqlTransaction - rolled back");

            throw;
        }
    }
}
