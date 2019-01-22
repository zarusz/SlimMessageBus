using System;
using System.Threading.Tasks;

namespace SlimMessageBus.Host
{
    public interface IMessageProcessor<in TMessage> : IDisposable
        where TMessage : class
    {
        Task ProcessMessage(TMessage message);
    }
}