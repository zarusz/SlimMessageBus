namespace SlimMessageBus.Host.Interceptor;

public interface IRequestHandlerInterceptor<in TRequest, TResponse> : IInterceptor
{
    /// <summary>
    /// Intercepts the OnHandle operation on request handler for the given request message type. The interceptor is invoked on the process that is consuming the message.
    /// </summary>
    /// <param name="request">The request message</param>
    /// <param name="next">Next step to execute (the message production or another interceptor)</param>
    /// <param name="context">The consumer context</param>
    /// <returns></returns>
    Task<TResponse> OnHandle(TRequest request, Func<Task<TResponse>> next, IConsumerContext context);
}
