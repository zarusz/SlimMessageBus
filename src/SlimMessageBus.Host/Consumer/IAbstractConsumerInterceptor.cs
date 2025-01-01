namespace SlimMessageBus.Host;

/// <summary>
/// Interceptor for consumers that are of type <see cref="AbstractConsumer"/>.
/// </summary>
public interface IAbstractConsumerInterceptor : IInterceptorWithOrder
{
    /// <summary>
    /// Called to check if the consumer can be started.
    /// </summary>
    /// <param name="consumer"></param>
    /// <returns>True if the start is allowed</returns>
    Task<bool> CanStart(AbstractConsumer consumer);

    /// <summary>
    /// Called to check if the consumer can be stopped.
    /// </summary>
    /// <param name="consumer"></param>
    /// <returns>True if the stop is allowed</returns>
    Task<bool> CanStop(AbstractConsumer consumer);
}
