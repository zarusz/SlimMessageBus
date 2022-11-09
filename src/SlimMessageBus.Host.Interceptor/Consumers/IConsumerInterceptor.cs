namespace SlimMessageBus.Host.Interceptor;

public interface IConsumerInterceptor<in TMessage> : IInterceptor
{
    /// <summary>
    /// Intercepts the OnHandle operation on consumer for the given message type. The interceptor is invoked on the process that is consuming the message.
    /// </summary>
    /// <param name="message">The message</param>
    /// <param name="next">Next step to execute (the message production or another interceptor)</param>
    /// <param name="context">The consumer context</param>
    /// <returns></returns>
    Task<object> OnHandle(TMessage message, Func<Task<object>> next, IConsumerContext context);
}
