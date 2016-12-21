using System.Collections.Generic;

namespace SlimMessageBus.Host
{
    /// <summary>
    /// Responsible for resolving the list of handlers (<see cref="ISubscriber{TMessage}"/>).
    /// </summary>
    public interface ISubscriberResolver
    {
        /// <summary>
        /// Resolves the list of handles (<see cref="ISubscriber{TMessage}"/>).
        /// </summary>
        /// <typeparam name="TMessage"></typeparam>
        /// <returns></returns>
        IEnumerable<ISubscriber<TMessage>> Resolve<TMessage>();
    }
}