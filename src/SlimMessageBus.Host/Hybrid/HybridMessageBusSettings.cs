namespace SlimMessageBus.Host.Hybrid;

public class HybridMessageBusSettings
{
    /// <summary>
    /// When there are multiple matching bus for a publish message type, defines the mode of execution.
    /// </summary>
    /// <remarks>The default is <see cref="PublishExecutionMode.Sequential"/>.</remarks>
    public PublishExecutionMode PublishExecutionMode { get; set; } = PublishExecutionMode.Sequential;

    /// <summary>
    /// When a message type is being produced that has no matching child bus that can produce such a message, defines the mode of execution.
    /// </summary>
    /// <remarks>The default is <see cref="UndeclaredMessageTypeMode.RaiseOneTimeLog"/>.</remarks>
    public UndeclaredMessageTypeMode UndeclaredMessageTypeMode { get; set; } = UndeclaredMessageTypeMode.RaiseOneTimeLog;
}

public enum UndeclaredMessageTypeMode
{
    /// <summary>
    /// Nothing happens (silent failure).
    /// </summary>
    DoNothing = 0,
    /// <summary>
    /// An INFO log is generated for every message type that was encountered. THe log happens only once for each message type.
    /// </summary>
    RaiseOneTimeLog = 1,
    /// <summary>
    /// Raises an exception (every time message is produced).
    /// </summary>
    RaiseException = 2,
}

public enum PublishExecutionMode
{
    /// <summary>
    /// Execute the publish on the first bus, then on the next (one after another).
    /// </summary>
    Sequential = 0,
    /// <summary>
    /// Execute the publish on all of the buses in pararellel (non-deterministic order).
    /// </summary>
    Parallel = 2
}