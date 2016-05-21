using System;
using System.Collections.Generic;

namespace SlimMessageBus.Core
{
    /// <summary>
    /// Responsible for resolving the list of handlers (<see cref="IHandles{TMessage}"/>).
    /// </summary>
    public interface IHandlerResolver
    {
        /// <summary>
        /// Resolves the list of handles (<see cref="IHandles{TMessage}"/>).
        /// </summary>
        /// <typeparam name="TEvent"></typeparam>
        /// <returns></returns>
        IEnumerable<IHandles<TEvent>> Resolve<TEvent>();
    }
}