namespace SlimMessageBus.Host;

// doc:fragment:Interface
public interface IConsumerErrorHandler<in T>
{
    /// <summary>
    /// Executed when the message consumer (or handler) errors out. This interface allows to intercept and handle the exception. 
    /// Use the consumer context to get ahold of transport specific options to proceed (acknowledge/reject message).
    /// </summary>
    /// <param name="message">The message that failed to process.</param>
    /// <param name="retry">Performs another message processing try. The return value is relevant if the consumer was a request handler (it will be its response value). Ensure to pass the return value to the result of the error handler.</param>
    /// <param name="consumerContext">The consumer context for the message processing pipeline.</param>
    /// <param name="exception">Exception that occurred during message processing.</param>
    /// <returns>The error handling result.</returns>
    Task<ConsumerErrorHandlerResult> OnHandleError(T message, Func<Task<object>> retry, IConsumerContext consumerContext, Exception exception);
}
// doc:fragment:Interface
