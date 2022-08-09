namespace SlimMessageBus.Host;

public interface IConsumerControl
{
    /// <summary>
    /// Starts message consumption
    /// </summary>
    /// <returns></returns>
    Task Start();

    /// <summary>
    /// Stops message consumption
    /// </summary>
    /// <returns></returns>
    Task Stop();
}