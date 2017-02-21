using System;
using System.Threading.Tasks;

namespace SlimMessageBus
{
    /// <summary>
    /// Bus to work with request-response messages.
    /// </summary>
    /// <exception cref="InvalidConfigurationMessageBusException">When request-response configuration is invalid</exception>
    /// <exception cref="PublishMessageBusException">When sending of the message failed</exception>
    /// <exception cref="RequestHandlerFaultedMessageBusException">When the request handler fails during processing of this request message</exception>
    public interface IRequestResponseBus
    {
        /// <summary>
        /// Sends a request message. Default timeout for request type (or global req/resp default) will be used.
        /// </summary>
        /// <typeparam name="TResponseMessage">The response message type</typeparam>
        /// <param name="request">The request message</param>
        /// <param name="topic">The topic to send the request to. When null, the default topic for request message type (or global default) will be used.</param>
        /// <returns>Task that represents the pending request.</returns>
        Task<TResponseMessage> Send<TResponseMessage>(IRequestMessage<TResponseMessage> request, string topic = null);

        /// <summary>
        /// Sends a request message
        /// </summary>
        /// <typeparam name="TResponseMessage">The response message type</typeparam>
        /// <param name="request">The request message</param>
        /// <param name="timeout">The timespan after which the Send request will be cancelled if no response arrives.</param>
        /// <param name="topic">The topic to send the request to. When null, the default topic for request message type (or global default) will be used.</param>
        /// <returns>Task that represents the pending request.</returns>
        Task<TResponseMessage> Send<TResponseMessage>(IRequestMessage<TResponseMessage> request, TimeSpan timeout, string topic = null);
    }
}