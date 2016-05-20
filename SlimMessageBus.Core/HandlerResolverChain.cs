using System.Collections.Generic;
using System.Linq;

namespace SlimMessageBus.Core
{
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