namespace SlimMessageBus.Host.MsDependencyInjection;

/// <summary>
/// An SMB DI adapter for the scope <see cref="IServiceScope"/>.
/// </summary>
public class MsDependencyInjectionChildDependencyResolver : IChildDependencyResolver
{
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger<MsDependencyInjectionChildDependencyResolver> logger;
    private readonly IServiceScope serviceScope;
    private bool disposedValue;

    public IDependencyResolver Parent { get; }

    public MsDependencyInjectionChildDependencyResolver(IDependencyResolver parent, IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
    {
        Parent = parent;
        this.loggerFactory = loggerFactory;
        logger = loggerFactory.CreateLogger<MsDependencyInjectionChildDependencyResolver>();
        serviceScope = serviceProvider.CreateScope();
    }

    /// <inheritdoc/>
    public IChildDependencyResolver CreateScope()
    {
        logger.LogDebug("Creating child scope");
        return new MsDependencyInjectionChildDependencyResolver(this, serviceScope.ServiceProvider, loggerFactory);
    }
    /// <inheritdoc/>
    public virtual object Resolve(Type type)
    {
        logger.LogDebug("Resolving type {0}", type);
        return serviceScope.ServiceProvider.GetService(type);
    }

    #region Dispose

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                logger.LogDebug("Disposing scope");
                serviceScope.Dispose();
            }

            disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion
}
