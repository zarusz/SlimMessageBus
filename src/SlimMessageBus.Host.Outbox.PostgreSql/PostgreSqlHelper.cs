namespace SlimMessageBus.Host.PostgreSql;

public static partial class PostgreSqlHelper
{
    private static readonly HashSet<int> TransientErrorNumbers = new HashSet<int>
    {
        40001, // Serialization Failure (e.g., deadlocks or concurrent updates)
        40001, // Deadlock Detected
        8000,  // Connection Exception
        8003,  // Connection Does Not Exist
        8006,  // Connection Failure
        8001,  // SQL Client Unable to Establish SQL Connection
        8004,  // SQL Server Rejected Establishment of SQL Connection
        8007,  // Transaction Resolution Unknown
        53300, // Too Many Connections
        57003, // Cannot Connect Now (e.g., during database shutdown)
        55003  // Lock Not Available (due to resource contention)
    };

    public static async Task<TResult?> RetryIfError<TResult>(ILogger logger, RetrySettings retrySettings, Func<NpgsqlException, bool> shouldRetry, Func<Task<TResult?>> operation, CancellationToken token)
    {
        Exception? lastTransientException = null;
        var nextRetryInterval = retrySettings.RetryInterval;
        for (var tries = 1; tries <= retrySettings.RetryCount && !token.IsCancellationRequested; tries++)
        {
            try
            {
                if (tries > 1)
                {
                    LogError(logger, retrySettings.RetryCount, tries);
                    await Task.Delay(nextRetryInterval, token);
                    nextRetryInterval = nextRetryInterval.Multiply(retrySettings.RetryIntervalFactor);
                }
                var result = await operation();
                return result;
            }
            catch (NpgsqlException ex) when (shouldRetry(ex))
            {
                // transient SQL error - continue trying
                lastTransientException = ex;
                LogWillRetry(logger, ex.ErrorCode, ex);
            }
        }

        throw lastTransientException!;
    }

    public static Task<TResult?> RetryIfTransientError<TResult>(ILogger logger, RetrySettings retrySettings, Func<Task<TResult?>> operation, CancellationToken token) =>
        RetryIfError(logger, retrySettings, sqlEx => TransientErrorNumbers.Contains(sqlEx.ErrorCode), operation, token);

    public static Task RetryIfTransientError(ILogger logger, RetrySettings retrySettings, Func<Task> operation, CancellationToken token) =>
        RetryIfTransientError<object>(logger, retrySettings, async () => { await operation(); return null; }, token);

    #region Logging

    [LoggerMessage(
       EventId = 0,
       Level = LogLevel.Information,
       Message = "PostgreSQL error encountered. Will begin attempt number {RetryNumber} of {RetryCount} max...")]
    private static partial void LogError(ILogger logger, int retryCount, int retryNumber);

    [LoggerMessage(
       EventId = 1,
       Level = LogLevel.Debug,
       Message = "PostgreSQL error occurred {ErrorCode}. Will retry operation")]
    private static partial void LogWillRetry(ILogger logger, int errorCode, NpgsqlException e);

    #endregion
}

#if NETSTANDARD2_0

partial class PostgreSqlHelper
{
    private static partial void LogError(ILogger logger, int retryCount, int retryNumber)
        => logger.LogInformation("PostgreSQL error encountered. Will begin attempt number {RetryNumber} of {RetryCount} max...", retryNumber, retryCount);

    private static partial void LogWillRetry(ILogger logger, int errorCode, NpgsqlException e)
        => logger.LogDebug(e, "PostgreSQL error occurred {ErrorCode}. Will retry operation", errorCode);
}
#endif