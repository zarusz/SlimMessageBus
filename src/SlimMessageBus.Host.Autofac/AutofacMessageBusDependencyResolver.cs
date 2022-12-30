namespace SlimMessageBus.Host;

/// <summary>
/// An SMB DI adapter for the Autofac <see cref="ILifetimeScope"/>.
/// </summary>
public class AutofacMessageBusDependencyResolver : IDependencyResolver
{
    private readonly ILogger<AutofacMessageBusDependencyResolver> _logger;
    private readonly Func<IComponentContext> _componentContextFunc;
    private readonly IComponentContext _componentContext;

    public AutofacMessageBusDependencyResolver(Func<IComponentContext> componentContextFunc)
    {
        _componentContextFunc = componentContextFunc;
        _logger = componentContextFunc().ResolveOptional<ILogger<AutofacMessageBusDependencyResolver>>() ?? NullLogger<AutofacMessageBusDependencyResolver>.Instance;
    }

    protected AutofacMessageBusDependencyResolver(IComponentContext componentContext)
    {
        _componentContext = componentContext;
        _logger = componentContext.ResolveOptional<ILogger<AutofacMessageBusDependencyResolver>>() ?? NullLogger<AutofacMessageBusDependencyResolver>.Instance;
    }

    public object Resolve(Type type)
    {
        _logger.LogTrace("Resolving type {0}", type);
        var o = ComponentContext.Resolve(type);
        _logger.LogTrace("Resolved type {0} to instance {1}", type, o);
        return o;
    }

    public IChildDependencyResolver CreateScope()
    {
        if (!(ComponentContext is ILifetimeScope lifetimeScope))
        {
            throw new InvalidOperationException($"The supplied Autofac {nameof(IComponentContext)} does not implement {nameof(ILifetimeScope)}");
        }

        _logger.LogDebug("Creating child scope");
        var scope = lifetimeScope.BeginLifetimeScope();
        return new AutofacMessageBusChildDependencyResolver(this, scope);
    }

    protected IComponentContext ComponentContext => _componentContext ?? _componentContextFunc();
}
