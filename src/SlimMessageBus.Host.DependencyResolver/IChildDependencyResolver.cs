namespace SlimMessageBus.Host.DependencyResolver
{
    using System;

    public interface IChildDependencyResolver : IDependencyResolver, IDisposable
    {
        IDependencyResolver Parent { get; }
    }
}