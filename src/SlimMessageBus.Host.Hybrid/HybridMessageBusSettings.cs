namespace SlimMessageBus.Host.Hybrid;

public class HybridMessageBusSettings
{
    /// <summary>
    /// When there are multiple matching bus for a publish message type, defines the mode of execution.
    /// </summary>
    public PublishExecutionMode PublishExecutionMode { get; set; } = PublishExecutionMode.Sequential;
}

public enum PublishExecutionMode : int
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