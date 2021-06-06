namespace SlimMessageBus
{
    using System.Threading.Tasks;

    /// <summary>
    /// Handler for request messages in the request-response communication.
    /// </summary>
    /// <typeparam name="TRequest">The request message type</typeparam>
    /// <typeparam name="TResponse">The response message type</typeparam>
    public interface IRequestHandler<in TRequest, TResponse>
        where TRequest : IRequestMessage<TResponse>
    {
        /// <summary>
        /// Handles the incoming request message.
        /// </summary>
        /// <param name="request">The request message</param>
        /// <param name="path">Name of the topic (or queue) on which the request message arrived on</param>
        /// <returns></returns>
        Task<TResponse> OnHandle(TRequest request, string path);
    }
}