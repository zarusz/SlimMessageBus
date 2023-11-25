namespace SlimMessageBus.Host.Test.Common;

public static class MoqMatchers
{
    public static bool LogMessageMatcher(object formattedLogValueObject, Func<string, bool> messageMatch)
    {
        var logValues = formattedLogValueObject as IReadOnlyList<KeyValuePair<string, object>>;
        var originalFormat = logValues?.FirstOrDefault(logValue => logValue.Key == "{OriginalFormat}").Value.ToString();
        return originalFormat != null && messageMatch(originalFormat);
    }
}
