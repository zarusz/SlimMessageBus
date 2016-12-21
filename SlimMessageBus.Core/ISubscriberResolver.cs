using System;
using System.Collections.Generic;

namespace SlimMessageBus.Core
{
    /// <summary>
    /// Responsible for resolving the list of handlers (<see cref="ISubscriber{TMessage}"/>).
    /// </summary>
    public interface ISubscriberResolver
    {
        /// <summary>
        /// Resolves the list of handles (<see cref="ISubscriber{TMessage}"/>).
        /// </summary>
        /// <typeparam name="TEvent"></typeparam>
        /// <returns></returns>
        IEnumerable<ISubscriber<TEvent>> Resolve<TEvent>();
    }
}