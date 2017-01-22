using System;
using System.Threading.Tasks;

namespace SlimMessageBus
{
    /// <summary>
    /// The publisher interface of the MessageBus
    /// </summary>
    public interface IPublisher<in TMessage> : IDisposable
    {
        Task Publish(TMessage msg, string topic = null);
    }
}