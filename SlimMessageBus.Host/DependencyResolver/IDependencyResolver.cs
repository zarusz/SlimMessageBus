using System;
using System.Collections.Generic;

namespace SlimMessageBus.Host
{
    /// <summary>
    /// Responsible for resolving the list of handlers (<see cref="ISubscriber{TMessage}"/>).
    /// </summary>
    public interface IDependencyResolver
    {
        /// <summary>
        /// Resolves the list of handles (<see cref="ISubscriber{TMessage}"/>).
        /// </summary>
        /// <returns></returns>
        IEnumerable<object> Resolve(Type type);
    }
}