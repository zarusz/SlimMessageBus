namespace SlimMessageBus.Host.Interceptor
{
    public interface IInterceptorWithOrder  : IInterceptor
    {
        int Order { get; }
    }
}
