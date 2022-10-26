namespace SlimMessageBus.Host.Interceptor;

public interface IProducerInterceptor<in TMessage> : IInterceptor
{
    /// <summary>
    /// Intercepts the Publish or Send operation on the bus for the given message type. The interceptor is invoked on the process that initiated the Publish or Send.
    /// </summary>
    /// <param name="message">The message</param>
    /// <param name="next">Next step to execute (the message production or another interceptor)</param>
    /// <param name="context">The producer context</param>
    /// <returns>In case of Publish the value is not used. In case of Send (request-response) you need to pass the response from next delegate or override the response altogether.</returns>
    Task<object> OnHandle(TMessage message, Func<Task<object>> next, IProducerContext context);
}
