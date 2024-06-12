namespace SlimMessageBus.Host.Sql.Common;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

public static class SqlHelper
{
    private static readonly HashSet<int> TransientErrorNumbers =
    [
        4060, 40197, 40501, 40613, 49918, 49919, 49920, 11001
    ];

    public static async Task<TResult> RetryIfError<TResult>(ILogger logger, SqlRetrySettings retrySettings, Func<SqlException, bool> shouldRetry, Func<Task<TResult>> operation, CancellationToken token)
    {
        Exception lastTransientException = null;
        var nextRetryInterval = retrySettings.RetryInterval;
        for (var tries = 1; tries <= retrySettings.RetryCount && !token.IsCancellationRequested; tries++)
        {
            try
            {
                if (tries > 1)
                {
                    logger.LogInformation("SQL error encountered. Will begin attempt number {SqlRetryNumber} of {SqlRetryCount} max...", tries, retrySettings.RetryCount);
                    await Task.Delay(nextRetryInterval, token);
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
                logger.LogDebug(sqlEx, "SQL error occurred {SqlErrorCode}. Will retry operation", sqlEx.Number);
            }
        }
        throw lastTransientException;
    }

    public static Task<TResult> RetryIfTransientError<TResult>(ILogger logger, SqlRetrySettings retrySettings, Func<Task<TResult>> operation, CancellationToken token) =>
        RetryIfError(logger, retrySettings, sqlEx => TransientErrorNumbers.Contains(sqlEx.Number), operation, token);

    public static Task RetryIfTransientError(ILogger logger, SqlRetrySettings retrySettings, Func<Task> operation, CancellationToken token) =>
        RetryIfTransientError<object>(logger, retrySettings, async () => { await operation(); return null; }, token);
}
