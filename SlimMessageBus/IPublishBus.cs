using System.Threading.Tasks;

namespace SlimMessageBus
{
    /// <summary>
    /// Bus to publish messages (pub/sub)
    /// </summary>
    public interface IPublishBus
    {
        /// <summary>
        /// Publish a message of the specified type.
        /// </summary>
        /// <typeparam name="TMessage"></typeparam>
        /// <param name="message">The message</param>
        /// <param name="topic">Topic to publish the message to. When null the default topic for the message type will be applied.</param>
        /// <returns></returns>
        /// <exception cref="InvalidConfigurationMessageBusException">When bus configuration is invalid</exception>
        /// <exception cref="PublishMessageBusException">When sending of the message failed</exception>
        Task Publish<TMessage>(TMessage message, string topic = null);
    }
}