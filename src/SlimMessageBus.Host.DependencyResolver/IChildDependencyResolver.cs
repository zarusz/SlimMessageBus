namespace SlimMessageBus.Host.DependencyResolver;

public interface IChildDependencyResolver : IDependencyResolver, IDisposable
{
    IDependencyResolver Parent { get; }
}