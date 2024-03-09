namespace SlimMessageBus.Host;

// doc:fragment:Interface
public interface IConsumerErrorHandler<in T>
{
    /// <summary>
    /// Executed when the message consumer errors out. This interface allows to intercept and handle the exception. 
    /// Use the consumer context to get ahold of transport specific options to proceed (acknowledge/reject message).
    /// </summary>
    /// <param name="message">The message that failed to process.</param>
    /// <param name="retry">Performs another message processing try.</param>
    /// <param name="consumerContext">The consumer context for the message processing pipeline.</param>
    /// <param name="exception">Exception that ocurred during message processing.</param>
    /// <returns>True, when the error was handled</returns>
    Task<bool> OnHandleError(T message, Func<Task> retry, IConsumerContext consumerContext, Exception exception);
}
// doc:fragment:Interface