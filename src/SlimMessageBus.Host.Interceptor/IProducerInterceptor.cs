namespace SlimMessageBus.Host.Interceptor
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IProducerInterceptor<in TMessage> : IInterceptor
    {
        /// <summary>
        /// Intercepts the Publish or Send operation on the bus for the given message type. The interceptor is invoked on the process that initiated the Publish or Send.
        /// </summary>
        /// <param name="message">The message</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="next">Next step to execute (the message production or another interceptor)</param>
        /// <param name="bus">The bus on which this interceptor is invoked</param>
        /// <param name="path">The path that will be used to send the message to</param>
        /// <param name="headers">Headers that are part of the message. The interceptor can mutate headers.</param>
        /// <returns>In case of Publish the value is not used. In case of Send (request-response) you need to pass the response from next delegate or override the response altogether.</returns>
        Task<object> OnHandle(TMessage message, CancellationToken cancellationToken, Func<Task<object>> next, IMessageBus bus, string path, IDictionary<string, object> headers);
    }
}
