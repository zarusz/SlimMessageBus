namespace SlimMessageBus.Host.Interceptor
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IPublishInterceptor<in TMessage> : IInterceptor
    {
        /// <summary>
        /// Intercepts the Publish operation on the bus for the given message type. The interceptor is invoked on the process that initiated the Publish or Send.
        /// </summary>
        /// <param name="message">The message</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="next">Next step to execute (the message production or another interceptor)</param>
        /// <param name="bus">The bus on which this interceptor is invoked</param>
        /// <param name="path">The path that will be used to send the message to</param>
        /// <param name="headers">Headers that are part of the message. The interceptor can mutate headers.</param>
        /// <returns></returns>
        Task OnHandle(TMessage message, CancellationToken cancellationToken, Func<Task> next, IMessageBus bus, string path, IDictionary<string, object> headers);
    }
}
