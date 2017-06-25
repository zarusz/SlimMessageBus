using System;

namespace SlimMessageBus.Host
{

    /// <summary>
    /// Simple implementation for <see cref="IDependencyResolver"/> that used a Func to lookup the depedency.
    /// </summary>
    public class LookupDependencyResolver : IDependencyResolver
    {
        private readonly Func<Type, object> _lookup;

        public LookupDependencyResolver(Func<Type, object> lookup)
        {
            _lookup = lookup;
        }

        #region Implementation of IDependencyResolver

        public object Resolve(Type type)
        {
            return _lookup(type);
        }

        #endregion
    }
}
