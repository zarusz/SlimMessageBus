namespace SlimMessageBus.Host.Outbox.Sql;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

public static class SqlHelper
{
    private static readonly ISet<int> TransientErrorNumbers = new HashSet<int>
    {
        4060, 40197, 40501, 40613, 49918, 49919, 49920, 11001
    };

    public static async Task<TResult> RetryIfError<TResult>(ILogger logger, CancellationToken token, SqlRetrySettings retrySettings, Func<SqlException, bool> shouldRetry, Func<Task<TResult>> operation)
    {
        Exception lastTransientException = null;
        var nextRetryInterval = retrySettings.RetryInterval;
        for (var tries = 1; tries <= retrySettings.RetryCount && !token.IsCancellationRequested; tries++)
        {
            try
            {
                if (tries > 1)
                {
                    logger.LogInformation("SQL error encountered. Will begin attempt number {0} of {1} max...", tries, retrySettings.RetryCount);
                    await Task.Delay(nextRetryInterval);
                    nextRetryInterval = nextRetryInterval.Multiply(retrySettings.RetryIntervalFactor);
                }
                var result = await operation();
                return result;
            }
            catch (SqlException sqlEx)
            {
                if (!shouldRetry(sqlEx))
                {
                    // non transient SQL error - report exception
                    throw;
                }
                // transient SQL error - continue trying
                lastTransientException = sqlEx;
                logger.LogDebug(sqlEx, "SQL error occured {SqlErrorCode}. Will retry operation", sqlEx.Number);
            }
        }
        throw lastTransientException;
    }

    public static Task<TResult> RetryIfTransientError<TResult>(ILogger logger, CancellationToken token, SqlRetrySettings retrySettings, Func<Task<TResult>> operation) =>
        RetryIfError(logger, token, retrySettings, sqlEx => TransientErrorNumbers.Contains(sqlEx.Number), operation);

    public static Task RetryIfTransientError(ILogger logger, CancellationToken token, SqlRetrySettings retrySettings, Func<Task> operation) =>
        RetryIfTransientError<object>(logger, token, retrySettings, async () => { await operation(); return null; });
}
