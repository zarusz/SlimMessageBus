using System.Collections.Generic;
using System.Linq;

namespace SlimMessageBus.Core
{
    /// <summary>
    /// Implementation of <see cref="IHandlerResolver"/> that delegates the resolve operation to a chain of other <see cref="IHandlerResolver"/>-s.
    /// The resolvers are controbuting to the list in the order of the chain (e.g first resolver-s handlers are first on the resolved list).
    /// </summary>
    public class HandlerResolverChain : IHandlerResolver
    {
        private readonly IList<IHandlerResolver> _chain = new List<IHandlerResolver>();

        public void Add(IHandlerResolver handlerResolver)
        {
            _chain.Add(handlerResolver);
        }

        #region Implementation of IHandlerResolver

        public IEnumerable<IHandles<TEvent>> Resolve<TEvent>()
        {
            IEnumerable<IHandles<TEvent>> allHandlers = null;

            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var handlerResolver in _chain)
            {
                var handlers = handlerResolver.Resolve<TEvent>();
                allHandlers = allHandlers?.Concat(handlers) ?? handlers;
            }

            return allHandlers;
        }

        #endregion
    }
}