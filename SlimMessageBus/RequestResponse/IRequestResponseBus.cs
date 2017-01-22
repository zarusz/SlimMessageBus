using System;
using System.Threading.Tasks;

namespace SlimMessageBus
{
    public interface IRequestResponseBus
    {
        Task<TResponseMessage> Send<TResponseMessage>(IRequestMessage<TResponseMessage> request, string topic = null);
        Task<TResponseMessage> Send<TResponseMessage>(IRequestMessage<TResponseMessage> request, TimeSpan timeout, string topic = null);
    }
}