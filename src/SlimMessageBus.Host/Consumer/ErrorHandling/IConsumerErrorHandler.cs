namespace SlimMessageBus.Host;

// doc:fragment:Interface
public interface IConsumerErrorHandler<in T>
{
    /// <summary>
    /// <para>
    /// Executed when the message consumer (or handler) errors out. The interface allows for interception of 
    /// exceptions to manipulate the processing pipeline (success/fail/retry).
    /// </para>
    /// <para>
    /// The consumer context is available to apply transport specific operations (acknowledge/reject/dead letter/etc).
    /// </para>
    /// <para>
    /// If message execution is to be re-attempted, any delays/jitter should be applied before the method returns.
    /// </para>
    /// </summary>
    /// <param name="message">The message that failed to process.</param>
    /// <param name="consumerContext">The consumer context for the message processing pipeline.</param>
    /// <param name="exception">Exception that occurred during message processing.</param>
    /// <param name="attempts">The number of times the message has been attempted to be processed.</param>
    /// <returns>The error handling result.</returns>
    Task<ProcessResult> OnHandleError(T message, IConsumerContext consumerContext, Exception exception, int attempts);
}
// doc:fragment:Interface