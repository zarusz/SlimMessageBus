using System;
using System.Collections.Generic;
using System.Linq;

namespace SlimMessageBus.Host
{
    /// <summary>
    /// Implementation of <see cref="IDependencyResolver"/> that delegates the resolve operation to a chain of other <see cref="IDependencyResolver"/>-s.
    /// The resolvers are controbuting to the list in the order of the chain (e.g first resolver-s handlers are first on the resolved list).
    /// </summary>
    public class DependencyResolverChain : IDependencyResolver
    {
        private readonly IList<IDependencyResolver> _chain = new List<IDependencyResolver>();

        public void Add(IDependencyResolver resolver)
        {
            _chain.Add(resolver);
        }

        #region Implementation of IDependencyResolver

        public IEnumerable<object> Resolve(Type type)
        {
            IEnumerable<object> allSubscribers = null;

            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var resolver in _chain)
            {
                var subscribers = resolver.Resolve(type);
                allSubscribers = allSubscribers?.Concat(subscribers) ?? subscribers;
            }

            return allSubscribers;
        }

        #endregion
    }
}