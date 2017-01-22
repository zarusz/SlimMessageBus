using System.Threading.Tasks;

namespace SlimMessageBus
{
    public interface IPublishBus
    {
        /// <summary>
        /// Publish a message of the specified type.
        /// </summary>
        /// <typeparam name="TMessage"></typeparam>
        /// <param name="message"></param>
        /// <param name="topic">Topic to publish the message to. When null the default topic for the message type will be applied.</param>
        /// <returns></returns>
        /// <exception cref="PublishMessageBusException"></exception>
        Task Publish<TMessage>(TMessage message, string topic = null);
    }
}