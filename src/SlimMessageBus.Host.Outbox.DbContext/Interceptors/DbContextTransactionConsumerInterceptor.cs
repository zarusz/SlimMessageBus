namespace SlimMessageBus.Host.Outbox.DbContext;

public abstract class DbContextTransactionConsumerInterceptor
{
}

/// <summary>
/// Wraps the consumer in an <see cref="DbTransaction"/> (conditionally) obtained from <see cref="Microsoft.EntityFrameworkCore.DbContext.Database"/>.
/// </summary>
/// <typeparam name="T"></typeparam>
public class DbContextTransactionConsumerInterceptor<T>(ILogger<DbContextTransactionConsumerInterceptor> logger, IOutboxMessageRepository outboxMessageRepository)
    : IConsumerInterceptor<T> where T : class
{
    public async Task<object> OnHandle(T message, Func<Task<object>> next, IConsumerContext context)
    {
        logger.LogTrace("SqlTransaction - creating...");
        var hasDbContext = (IHasDbContext)outboxMessageRepository;
        var dbTransaction = await hasDbContext.DbContext.Database.BeginTransactionAsync(context.CancellationToken);
        try
        {
            logger.LogDebug("SqlTransaction - created");

            var result = await next();

            logger.LogTrace("SqlTransaction - committing...");
            await dbTransaction.CommitAsync(context.CancellationToken);
            logger.LogDebug("SqlTransaction - committed");
            return result;
        }
        catch
        {
            logger.LogTrace("SqlTransaction - rolling back...");
            await dbTransaction.RollbackAsync(context.CancellationToken);
            logger.LogDebug("SqlTransaction - rolled back");
            throw;
        }
    }
}
