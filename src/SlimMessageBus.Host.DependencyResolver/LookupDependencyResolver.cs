namespace SlimMessageBus.Host.DependencyResolver
{
    using System;

    /// <summary>
    /// Simple implementation for <see cref="IDependencyResolver"/> that used a Func to lookup the dependency.
    /// </summary>
    public class LookupDependencyResolver : IDependencyResolver
    {
        private readonly Func<Type, object> lookup;

        public LookupDependencyResolver(Func<Type, object> lookup) => this.lookup = lookup;

        public IChildDependencyResolver CreateScope() => new ChildDependencyResolver(this);

        public object Resolve(Type type) => lookup(type);

        private class ChildDependencyResolver : IChildDependencyResolver
        {
            public IDependencyResolver Parent { get; }

            public ChildDependencyResolver(IDependencyResolver parent) => Parent = parent;

            public object Resolve(Type type) => Parent.Resolve(type);
            
            public IChildDependencyResolver CreateScope() => new ChildDependencyResolver(this);

            public void Dispose()
            {
                // Do Nothing
            }

        }
    }
}
