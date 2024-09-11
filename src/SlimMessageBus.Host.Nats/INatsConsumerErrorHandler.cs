namespace SlimMessageBus.Host.Nats;

public interface INatsConsumerErrorHandler<in T> : IConsumerErrorHandler<T>
{
}