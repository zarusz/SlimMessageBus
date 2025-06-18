namespace SlimMessageBus.Host;

public class UndeclaredMessageTypeSettings
{
    /// <summary>
    /// Should the message fail when an undeclared message type arrives on the queue/topic that cannot be handled by any of the declared consumers.
    /// By default is true.
    /// </summary>
    public bool? Fail { get; set; }
    /// <summary>
    /// Should the message be logged when an undeclared message type arrives on the queue/topic that cannot be handled by any of the declared consumers.
    /// By default is false.
    /// </summary>
    public bool? Log { get; set; }
}