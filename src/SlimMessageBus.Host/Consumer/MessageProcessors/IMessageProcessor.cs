namespace SlimMessageBus.Host
{
    using SlimMessageBus.Host.Config;
    using System;
    using System.Threading.Tasks;

    public interface IMessageProcessor<TMessage> : IDisposable where TMessage : class
    {
        AbstractConsumerSettings ConsumerSettings { get; }
        
        /// <summary>
        /// Processes the arrived message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="consumerInvoker"></param>
        /// <returns>Null, if message processing was sucessful, otherwise the Exception</returns>
        Task<Exception> ProcessMessage(TMessage message, IMessageTypeConsumerInvokerSettings consumerInvoker = null);
    }
}