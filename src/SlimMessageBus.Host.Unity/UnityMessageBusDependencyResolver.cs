namespace SlimMessageBus.Host;

using Unity;

/// <summary>
/// An SMB DI adapter for the <see cref="IUnityContainer"/>.
/// </summary>
public class UnityMessageBusDependencyResolver : IDependencyResolver
{
    private readonly ILogger<UnityMessageBusDependencyResolver> _logger;
    protected IUnityContainer Container { get; }

    public UnityMessageBusDependencyResolver(IUnityContainer container)
    {
        _logger = container.Resolve<ILogger<UnityMessageBusDependencyResolver>>();
        Container = container;
    }

    public object Resolve(Type type)
    {
        _logger.LogTrace("Resolving type {Type}", type);
        var o = Container.Resolve(type);
        _logger.LogTrace("Resolved type {Type} to instance {Instance}", type, o);
        return o;
    }

    public IChildDependencyResolver CreateScope()
    {
        _logger.LogDebug("Creating child scope");
        var childContainer = Container.CreateChildContainer();
        return new UnityMessageBusChildDependencyResolver(this, childContainer);
    }
}