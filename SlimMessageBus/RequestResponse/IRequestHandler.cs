using System.Threading.Tasks;

namespace SlimMessageBus
{
    public interface IRequestHandler<in TRequest, TResponse>
        where TResponse : IResponseMessage
        where TRequest : IRequestMessageWithResponse<TResponse>
    {
        Task<TResponse> OnHandle(TRequest message);
    }
}