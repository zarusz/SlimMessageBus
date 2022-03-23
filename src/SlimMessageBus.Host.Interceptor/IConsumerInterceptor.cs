namespace SlimMessageBus.Host.Interceptor
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IConsumerInterceptor<in TMessage> : IInterceptor
    {
        Task OnHandle(TMessage message, CancellationToken cancellationToken, Func<Task> next, IMessageBus bus, string path, IReadOnlyDictionary<string, object> headers, object consumer);
    }
}
