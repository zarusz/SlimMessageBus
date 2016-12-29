using System.Threading.Tasks;

namespace SlimMessageBus
{
    public interface IRequestHandler<in TRequest, TResponse>
        where TRequest : IRequestMessage<TResponse>
    {
        Task<TResponse> OnHandle(TRequest message);
    }
}