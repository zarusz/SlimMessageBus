namespace SlimMessageBus.Host.Consumer.ErrorHandling;

public abstract class ConsumerErrorHandler<T> : BaseConsumerErrorHandler, IConsumerErrorHandler<T>
{
    public abstract Task<ProcessResult> OnHandleError(T message, IConsumerContext consumerContext, Exception exception, int attempts);
}

public abstract class BaseConsumerErrorHandler
{
    public virtual ProcessResult Failure() => ProcessResult.Failure;
    public virtual ProcessResult Retry() => ProcessResult.Retry;
    public virtual ProcessResult Success(object response = null) => response == null ? ProcessResult.Success : ProcessResult.SuccessWithResponse(response);
}