namespace SlimMessageBus.Host.Outbox.PostgreSql.DbContext.Test;

internal static partial class Log
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Message Publish took       : {Elapsed}")]
    internal static partial void LogMessagePublishTook(this ILogger logger, TimeSpan elapsed);

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox Publish took        : {Elapsed}")]
    internal static partial void LogOutboxPublishTook(this ILogger logger, TimeSpan elapsed);

    [LoggerMessage(Level = LogLevel.Information, Message = "Message Consume took       : {Elapsed}")]
    internal static partial void LogMessageConsumeTook(this ILogger logger, TimeSpan elapsed);

    [LoggerMessage(Level = LogLevel.Information, Message = "Received {ActualCount}/{ExpectedCount} messages after initial wait")]
    internal static partial void LogReceivedMessages(this ILogger logger, int actualCount, int expectedCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Exception occurred while handling cmd {Command}: {ExceptionMessage}")]
    internal static partial void LogCommandException(this ILogger logger, object command, string exceptionMessage);
}
