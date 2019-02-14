using System.Threading.Tasks;

namespace SlimMessageBus
{
    /// <summary>
    /// Bus to publish messages
    /// </summary>
    public interface IPublishBus
    {
        /// <summary>
        /// Publish a message of the specified type.
        /// </summary>
        /// <typeparam name="TMessage"></typeparam>
        /// <param name="message">The message</param>
        /// <param name="name">Name of topic (or queue) to publish the message to. When null the default name for the message type <see cref="TMessage"/> will be applied.</param>
        /// <returns></returns>
        /// <exception cref="InvalidConfigurationMessageBusException">When bus configuration is invalid</exception>
        /// <exception cref="PublishMessageBusException">When sending of the message failed</exception>
        Task Publish<TMessage>(TMessage message, string name = null);
    }
}