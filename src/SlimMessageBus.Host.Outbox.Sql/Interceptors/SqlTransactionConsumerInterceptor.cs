namespace SlimMessageBus.Host.Outbox.Sql;

using Microsoft.Extensions.Logging;

using SlimMessageBus;
using SlimMessageBus.Host.Interceptor;

public abstract class SqlTransactionConsumerInterceptor
{
}

/// <summary>
/// Wraps the consumer in an <see cref="TransactionScope"/> (conditionally).
/// </summary>
/// <typeparam name="T"></typeparam>
public class SqlTransactionConsumerInterceptor<T>(
    ILogger<SqlTransactionConsumerInterceptor> logger,
    ISqlTransactionService transactionService)
    : SqlTransactionConsumerInterceptor, IConsumerInterceptor<T> where T : class
{
    public async Task<object> OnHandle(T message, Func<Task<object>> next, IConsumerContext context)
    {
        var sqlTransactionEnabled = IsSqlTransactionEnabled(context);
        if (sqlTransactionEnabled)
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

        return await next();
    }

    private static bool IsSqlTransactionEnabled(IConsumerContext context)
    {
        var bus = context.GetMasterMessageBus();
        if (bus == null || context is not ConsumerContext consumerContext)
        {
            return false;
        }

        // If consumer has outbox enabled, if not set check if bus has outbox enabled
        var transactionEnabled = consumerContext.ConsumerInvoker.ParentSettings.GetOrDefault<bool?>(BuilderExtensions.PropertySqlTransactionEnabled, null)
            ?? bus.Settings.GetOrDefault(BuilderExtensions.PropertySqlTransactionEnabled, false);

        return transactionEnabled;
    }
}
