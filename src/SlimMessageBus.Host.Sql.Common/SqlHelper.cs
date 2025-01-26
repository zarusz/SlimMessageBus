namespace SlimMessageBus.Host.Sql.Common;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

public static partial class SqlHelper
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
                    LogSqlError(logger, retrySettings.RetryCount, tries);
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
                LogWillRetry(logger, sqlEx.Number, sqlEx);
            }
        }
        throw lastTransientException;
    }

    public static Task<TResult> RetryIfTransientError<TResult>(ILogger logger, SqlRetrySettings retrySettings, Func<Task<TResult>> operation, CancellationToken token) =>
        RetryIfError(logger, retrySettings, sqlEx => TransientErrorNumbers.Contains(sqlEx.Number), operation, token);

    public static Task RetryIfTransientError(ILogger logger, SqlRetrySettings retrySettings, Func<Task> operation, CancellationToken token) =>
        RetryIfTransientError<object>(logger, retrySettings, async () => { await operation(); return null; }, token);

    #region Logging

    [LoggerMessage(
       EventId = 0,
       Level = LogLevel.Information,
       Message = "SQL error encountered. Will begin attempt number {SqlRetryNumber} of {SqlRetryCount} max...")]
    private static partial void LogSqlError(ILogger logger, int sqlRetryCount, int sqlRetryNumber);

    [LoggerMessage(
       EventId = 1,
       Level = LogLevel.Debug,
       Message = "SQL error occurred {SqlErrorCode}. Will retry operation")]
    private static partial void LogWillRetry(ILogger logger, int sqlErrorCode, SqlException e);

    #endregion
}

#if NETSTANDARD2_0

partial class SqlHelper
{
    private static partial void LogSqlError(ILogger logger, int sqlRetryCount, int sqlRetryNumber)
        => logger.LogInformation("SQL error encountered. Will begin attempt number {SqlRetryNumber} of {SqlRetryCount} max...", sqlRetryNumber, sqlRetryCount);

    private static partial void LogWillRetry(ILogger logger, int sqlErrorCode, SqlException e)
        => logger.LogDebug(e, "SQL error occurred {SqlErrorCode}. Will retry operation", sqlErrorCode);
}

#endif