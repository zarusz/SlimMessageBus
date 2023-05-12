namespace SlimMessageBus.Host.RabbitMQ;

public abstract class RabbitMqConsumerErrorHandler<T> : IConsumerErrorHandler<T>, IConsumerErrorHandlerUntyped
{
    /// <inheritdoc/>
    public abstract Task<bool> OnHandleError(T message, IConsumerContext consumerContext, Exception exception);

    public Task<bool> OnHandleError(object message, IConsumerContext consumerContext, Exception exception) => OnHandleError((T)message, consumerContext, exception);
}