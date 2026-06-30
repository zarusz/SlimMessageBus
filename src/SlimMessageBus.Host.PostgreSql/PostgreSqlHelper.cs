namespace SlimMessageBus.Host.PostgreSql;

public static partial class PostgreSqlHelper
{
    private static readonly HashSet<int> TransientErrorNumbers =
    [
        40001,
        8000,
        8001,
        8003,
        8004,
        8006,
        8007,
        53300,
        55003,
        57003
    ];

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
                    await Task.Delay(nextRetryInterval, token).ConfigureAwait(false);
                    nextRetryInterval = nextRetryInterval.Multiply(retrySettings.RetryIntervalFactor);
                }
                return await operation().ConfigureAwait(false);
            }
            catch (NpgsqlException ex) when (shouldRetry(ex))
            {
                lastTransientException = ex;
                LogWillRetry(logger, ex.ErrorCode, ex);
            }
        }

        throw lastTransientException!;
    }

    public static Task<TResult?> RetryIfTransientError<TResult>(ILogger logger, RetrySettings retrySettings, Func<Task<TResult?>> operation, CancellationToken token) =>
        RetryIfError(logger, retrySettings, sqlEx => TransientErrorNumbers.Contains(sqlEx.ErrorCode), operation, token);

    public static Task RetryIfTransientError(ILogger logger, RetrySettings retrySettings, Func<Task> operation, CancellationToken token) =>
        RetryIfTransientError<object>(logger, retrySettings, async () => { await operation().ConfigureAwait(false); return null; }, token);

    public static string QuoteIdentifier(string identifier, string name)
    {
        if (!IsSafeIdentifier(identifier))
        {
            throw new ArgumentException("PostgreSQL identifiers can only contain letters, numbers, and underscores, and cannot start with a number.", name);
        }

        return $"\"{identifier}\"";
    }

    private static bool IsSafeIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier) || !(char.IsLetter(identifier[0]) || identifier[0] == '_'))
        {
            return false;
        }

        for (var i = 1; i < identifier.Length; i++)
        {
            var c = identifier[i];
            if (!(char.IsLetterOrDigit(c) || c == '_'))
            {
                return false;
            }
        }

        return true;
    }

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
}

#if NETSTANDARD2_0

partial class PostgreSqlHelper
{
    private static partial void LogError(ILogger logger, int retryCount, int retryNumber)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("PostgreSQL error encountered. Will begin attempt number {RetryNumber} of {RetryCount} max...", retryNumber, retryCount);
        }
    }

    private static partial void LogWillRetry(ILogger logger, int errorCode, NpgsqlException e)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(e, "PostgreSQL error occurred {ErrorCode}. Will retry operation", errorCode);
        }
    }
}
#endif
