namespace SlimMessageBus.Host;

public interface IConsumerErrorHandler<in T>
{
    /// <summary>
    /// Executed when the message consumer errors out. This interface allows to intercept and handle the exception. 
    /// Use the consumer context to get ahold of transport specific options to proceed (acknowledge/reject message, retry).
    /// </summary>
    /// <param name="message"></param>
    /// <param name="consumerContext"></param>
    /// <param name="exception"></param>
    /// <returns>True, when the error was handled</returns>
    Task<bool> OnHandleError(T message, IConsumerContext consumerContext, Exception exception);
}
