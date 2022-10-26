namespace SlimMessageBus.Host.Interceptor;

public interface IPublishInterceptor<in TMessage> : IInterceptor
{
    /// <summary>
    /// Intercepts the Publish operation on the bus for the given message type. The interceptor is invoked on the process that initiated the Publish or Send.
    /// </summary>
    /// <param name="message">The message</param>
    /// <param name="next">Next step to execute (the message production or another interceptor)</param>
    /// <param name="context">The producer context</param>
    /// <returns></returns>
    Task OnHandle(TMessage message, Func<Task> next, IProducerContext context);
}
