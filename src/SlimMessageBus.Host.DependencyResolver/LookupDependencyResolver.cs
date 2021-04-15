using System;

namespace SlimMessageBus.Host.DependencyResolver
{
    /// <summary>
    /// Simple implementation for <see cref="IDependencyResolver"/> that used a Func to lookup the dependency.
    /// </summary>
    public class LookupDependencyResolver : IDependencyResolver
    {
        private readonly Func<Type, object> _lookup;

        public LookupDependencyResolver(Func<Type, object> lookup)
        {
            _lookup = lookup;
        }

        public IDependencyResolver CreateScope() => this;

        public void Dispose()
        {
        }

        public object Resolve(Type type) => _lookup(type);
    }
}
