namespace SlimMessageBus
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

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
        /// <param name="path">Name of topic (or queue) to publish the message to. When null the default name for the message type <see cref="TMessage"/> will be applied.</param>
        /// <param name="headers">The headers to add to the message. When null no additional headers will be added.</param>
        /// <param name="cancellationToken">The CancellationToken.</param>
        /// <returns></returns>
        /// <exception cref="PublishMessageBusException">When sending of the message failed</exception>
        Task Publish<TMessage>(TMessage message, string path = null, IDictionary<string, object> headers = null, CancellationToken cancellationToken = default);
    }
}