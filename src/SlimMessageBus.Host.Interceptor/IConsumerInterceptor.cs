namespace SlimMessageBus.Host.Interceptor;

public interface IConsumerInterceptor<in TMessage> : IInterceptor
{
    Task OnHandle(TMessage message, CancellationToken cancellationToken, Func<Task> next, IMessageBus bus, string path, IReadOnlyDictionary<string, object> headers, object consumer);
}
