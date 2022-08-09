namespace SlimMessageBus.Host.Interceptor;

public interface IRequestHandlerInterceptor<in TRequest, TResponse> : IInterceptor
{
    Task<TResponse> OnHandle(TRequest request, CancellationToken cancellationToken, Func<Task<TResponse>> next, IMessageBus bus, string path, IReadOnlyDictionary<string, object> headers, object handler);
}
