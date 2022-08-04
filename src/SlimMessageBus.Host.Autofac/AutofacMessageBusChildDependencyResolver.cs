namespace SlimMessageBus.Host;

public class AutofacMessageBusChildDependencyResolver : AutofacMessageBusDependencyResolver, IChildDependencyResolver
{
    private readonly ILogger<AutofacMessageBusDependencyResolver> logger;
    private readonly ILifetimeScope lifetimeScope;
    private bool disposedValue;

    public IDependencyResolver Parent { get; }

    public AutofacMessageBusChildDependencyResolver(IDependencyResolver parent, ILifetimeScope lifetimeScope)
        : base(lifetimeScope)
    {
        logger = lifetimeScope.ResolveOptional<ILogger<AutofacMessageBusChildDependencyResolver>>() ?? NullLogger<AutofacMessageBusChildDependencyResolver>.Instance;
        this.lifetimeScope = lifetimeScope;
        Parent = parent;
    }

    #region Dispose

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                if (lifetimeScope != null)
                {
                    logger.LogDebug("Disposing scope");
                    lifetimeScope.Dispose();
                }
            }

            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion
}

