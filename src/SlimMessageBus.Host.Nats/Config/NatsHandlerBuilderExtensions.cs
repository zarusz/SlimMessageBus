namespace SlimMessageBus.Host.Nats;

public static class NatsHandlerBuilderExtensions
{
    /// <summary>
    /// Configure queue name that incoming requests (<see cref="TRequest"/>) are expected on.
    /// </summary>
    /// <param name="natsBuilder"></param>
    /// <param name="natsQueue">Queue name</param>
    /// <returns></returns>
    public static HandlerBuilder<TRequest, TResponse> Queue<TRequest, TResponse>(this HandlerBuilder<TRequest, TResponse> natsBuilder, string natsQueue)
    {
        if (natsBuilder is null) throw new ArgumentNullException(nameof(natsBuilder));
        if (natsQueue is null) throw new ArgumentNullException(nameof(natsQueue));

        natsBuilder.Path(natsQueue);
        natsBuilder.ConsumerSettings.PathKind = PathKind.Queue;
        return natsBuilder;
    }
}