namespace SlimMessageBus.Host.Outbox;

public class OutboxMessageCleanupSettings
{
    /// <summary>
    /// Should the message cleanup be enabled?
    /// </summary>
    public bool Enabled { get; set; } = true; ddd
    /// <summary>
    /// Interval at which the sent message cleanup is performed.
    /// </summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);
    /// <summary>
    /// Message age from which the sent messages are removed from.
    /// </summary>
    public TimeSpan Age { get; set; } = TimeSpan.FromHours(1);
}