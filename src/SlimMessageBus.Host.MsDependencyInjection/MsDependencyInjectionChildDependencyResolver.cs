namespace SlimMessageBus.Host.MsDependencyInjection;

/// <summary>
/// An SMB DI adapter for the scope <see cref="IServiceScope"/>.
/// </summary>
public class MsDependencyInjectionChildDependencyResolver : IChildDependencyResolver
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<MsDependencyInjectionChildDependencyResolver> _logger;
    private readonly IServiceScope _serviceScope;
    private bool disposedValue;

    public IDependencyResolver Parent { get; }

    public MsDependencyInjectionChildDependencyResolver(IDependencyResolver parent, IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
    {
        Parent = parent;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<MsDependencyInjectionChildDependencyResolver>();
        _serviceScope = serviceProvider.CreateScope();
    }

    /// <inheritdoc/>
    public IChildDependencyResolver CreateScope()
    {
        _logger.LogDebug("Creating child scope");
        return new MsDependencyInjectionChildDependencyResolver(this, _serviceScope.ServiceProvider, _loggerFactory);
    }
    /// <inheritdoc/>
    public virtual object Resolve(Type type)
    {
        _logger.LogDebug("Resolving type {0}", type);
        return _serviceScope.ServiceProvider.GetService(type);
    }

    #region Dispose

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                _logger.LogDebug("Disposing scope");
                _serviceScope.Dispose();
            }

            disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        // Perform async cleanup.
        await DisposeAsyncCore().ConfigureAwait(false);
        Dispose(false);
        GC.SuppressFinalize(this);
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (_serviceScope is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
    }

    #endregion
}
