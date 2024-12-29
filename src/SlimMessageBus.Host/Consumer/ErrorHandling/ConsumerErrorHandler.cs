namespace SlimMessageBus.Host.Consumer.ErrorHandling;

public abstract class ConsumerErrorHandler<T> : BaseConsumerErrorHandler, IConsumerErrorHandler<T>
{
    public abstract Task<ConsumerErrorHandlerResult> OnHandleError(T message, IConsumerContext consumerContext, Exception exception, int attempts);
}

public abstract class BaseConsumerErrorHandler
{
    public static ConsumerErrorHandlerResult Failure() => ConsumerErrorHandlerResult.Failure;
    public static ConsumerErrorHandlerResult Retry() => ConsumerErrorHandlerResult.Retry;
    public static ConsumerErrorHandlerResult Success(object response = null) => response == null ? ConsumerErrorHandlerResult.Success : ConsumerErrorHandlerResult.SuccessWithResponse(response);
}