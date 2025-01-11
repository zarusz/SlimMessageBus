#if NETSTANDARD2_0

namespace SlimMessageBus.Host;

[AttributeUsage(AttributeTargets.Method)]
public class LoggerMessageAttribute : Attribute
{
    /// <summary>
    /// Gets the logging event id for the logging method.
    /// </summary>
    public int EventId { get; set; } = -1;

    /// <summary>
    /// Gets or sets the logging event name for the logging method.
    /// </summary>
    /// <remarks>
    /// This will equal the method name if not specified.
    /// </remarks>
    public string EventName { get; set; }

    /// <summary>
    /// Gets the logging level for the logging method.
    /// </summary>
    public LogLevel Level { get; set; } = LogLevel.None;

    /// <summary>
    /// Gets the message text for the logging method.
    /// </summary>
    public string Message { get; set; } = "";

    /// <summary>
    /// Gets the flag to skip IsEnabled check for the logging method.
    /// </summary>
    public bool SkipEnabledCheck { get; set; }

    public LoggerMessageAttribute()
    {
    }

    public LoggerMessageAttribute(int eventId, LogLevel level, string message)
    {
    }
}

#endif