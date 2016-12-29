using System;
using System.Threading.Tasks;

namespace SlimMessageBus
{
    public interface IRequestResponseBus
    {
        Task<TResponseMessage> Request<TResponseMessage>(IRequestMessage<TResponseMessage> request);
        Task<TResponseMessage> Request<TResponseMessage>(IRequestMessage<TResponseMessage> request, TimeSpan timeout);
    }
}