using System;
using System.Threading.Tasks;

namespace SlimMessageBus.Host
{
    public interface IMessageProcessor<in TMessage> : IDisposable where TMessage : class
    {
        /// <summary>
        /// Processes the arrived message
        /// </summary>
        /// <param name="message"></param>
        /// <returns>Null, if message processing was sucessful, otherwise the Exception</returns>
        Task<Exception> ProcessMessage(TMessage message);
    }
}