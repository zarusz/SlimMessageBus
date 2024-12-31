namespace SlimMessageBus.Host.Consumer.ErrorHandling;

public abstract class ConsumerErrorHandler<T> : BaseConsumerErrorHandler, IConsumerErrorHandler<T>
{
    public abstract Task<ConsumerErrorHandlerResult> OnHandleError(T message, IConsumerContext consumerContext, Exception exception, int attempts);
}

public abstract class BaseConsumerErrorHandler
{
    public virtual ConsumerErrorHandlerResult Abandon() => ConsumerErrorHandlerResult.Abandon;
    public virtual ConsumerErrorHandlerResult Failure() => ConsumerErrorHandlerResult.Failure;
    public virtual ConsumerErrorHandlerResult Retry() => ConsumerErrorHandlerResult.Retry;
    public virtual ConsumerErrorHandlerResult Success(object response = null) => response == null ? ConsumerErrorHandlerResult.Success : ConsumerErrorHandlerResult.SuccessWithResponse(response);
}