using System.Threading.Tasks;

namespace SlimMessageBus
{
    /// <summary>
    /// Handler for request messages.
    /// </summary>
    /// <typeparam name="TRequest"></typeparam>
    /// <typeparam name="TResponse"></typeparam>
    public interface IRequestHandler<in TRequest, TResponse>
        where TRequest : IRequestMessage<TResponse>
    {
        /// <summary>
        /// Handles the incomming request message.
        /// </summary>
        /// <param name="request">The request message</param>
        /// <param name="topic">The topic on which the request message arrived on.</param>
        /// <returns></returns>
        Task<TResponse> OnHandle(TRequest request, string topic);
    }
}