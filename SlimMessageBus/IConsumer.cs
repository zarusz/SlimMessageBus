using System.Threading.Tasks;

namespace SlimMessageBus
{
    /// <summary>
    /// Subscriber for messages of given type <typeparam name="TMessage"></typeparam>.
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    public interface IConsumer<in TMessage>
    {
        /// <summary>
        /// Invoked when a message arrives of type <typeparam name="TMessage"></typeparam>.
        /// </summary>
        /// <param name="message">The arriving message</param>
        /// <param name="topic">The topic the message arrived on</param>
        /// <returns></returns>
        Task OnHandle(TMessage message, string topic);
    }
}