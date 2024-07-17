namespace SlimMessageBus.Host;

public interface IConsumerControl
{
    /// <summary>
    /// Starts message consumption
    /// </summary>
    /// <returns></returns>
    Task Start();

    /// <summary>
    /// Indicates whether the consumers are started.
    /// </summary>
    bool IsStarted { get; }

    /// <summary>
    /// Stops message consumption
    /// </summary>
    /// <returns></returns>
    Task Stop();
}
