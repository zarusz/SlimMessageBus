namespace SlimMessageBus.Host;

public class AutofacMessageBusChildDependencyResolver : AutofacMessageBusDependencyResolver, IChildDependencyResolver
{
    private readonly ILogger<AutofacMessageBusDependencyResolver> _logger;
    private readonly ILifetimeScope _lifetimeScope;
    private bool _disposedValue;

    public IDependencyResolver Parent { get; }

    public AutofacMessageBusChildDependencyResolver(IDependencyResolver parent, ILifetimeScope lifetimeScope)
        : base(lifetimeScope)
    {
        _logger = lifetimeScope.ResolveOptional<ILogger<AutofacMessageBusChildDependencyResolver>>() ?? NullLogger<AutofacMessageBusChildDependencyResolver>.Instance;
        _lifetimeScope = lifetimeScope;
        Parent = parent;
    }

    #region Dispose

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                if (_lifetimeScope != null)
                {
                    _logger.LogDebug("Disposing scope");
                    _lifetimeScope.Dispose();
                }
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        Dispose(disposing: false);
        GC.SuppressFinalize(this);
    }

    protected async virtual ValueTask DisposeAsyncCore()
    {
        await _lifetimeScope.DisposeAsync().ConfigureAwait(false);
    }

    #endregion
}

