using System.Threading;

namespace SlimMessageBus.Host
{
    /// <summary>
    /// An extension point for <see cref="IConsumer{TMessage}"/> to recieve provider specific (for current message subject to processing).
    /// </summary>
    public interface IConsumerContextAware
    {
        /// <summary>
        /// Obtain current message consumer context.
        /// </summary>
        AsyncLocal<ConsumerContext> Context { get; }
    }
}
