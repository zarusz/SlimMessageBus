namespace SlimMessageBus.Host;

public interface IConsumerErrorHandlerUntyped
{
    Task<bool> OnHandleError(object message, IConsumerContext consumerContext, Exception exception);
}
