using System;
using System.Collections.Generic;

namespace SlimMessageBus.Host
{
    /// <summary>
    /// Responsible for resolving the list of handlers (<see cref="IConsumer{TMessage}"/>).
    /// </summary>
    public interface IDependencyResolver
    {
        /// <summary>
        /// Resolves the list of handles (<see cref="IConsumer{TMessage}"/>).
        /// </summary>
        /// <returns></returns>
        IEnumerable<object> Resolve(Type type);
    }
}