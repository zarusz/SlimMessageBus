namespace SlimMessageBus.Host.Interceptor;

public interface ISendInterceptor<in TRequest, TResponse> : IInterceptor
{
    /// <summary>
    /// Intercepts the Send operation on the bus for the given message type. The interceptor is invoked on the process that initiated the Send.
    /// </summary>
    /// <param name="request">The message</param>
    /// <param name="next">Next step to execute (the message production or another interceptor)</param>
    /// <param name="context">The producer context</param>
    /// <returns>The response (request-response). You need to pass the response from next delegate or override the response altogether.</returns>
    Task<TResponse> OnHandle(TRequest request, Func<Task<TResponse>> next, IProducerContext context);
}
