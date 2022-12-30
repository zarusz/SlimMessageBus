namespace SlimMessageBus.Host.DependencyResolver;

public interface IChildDependencyResolver : IDependencyResolver, IDisposable, IAsyncDisposable
{
    IDependencyResolver Parent { get; }
}