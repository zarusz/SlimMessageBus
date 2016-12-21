using System;
using System.Threading.Tasks;

namespace SlimMessageBus
{
    public interface IRequestResponseBus
    {
        Task<TResponseMessage> Request<TResponseMessage>(IRequestMessageWithResponse<TResponseMessage> request)
            where TResponseMessage : IResponseMessage;

        Task<TResponseMessage> Request<TResponseMessage>(IRequestMessageWithResponse<TResponseMessage> request, TimeSpan timeout)
            where TResponseMessage : IResponseMessage;
    }
}