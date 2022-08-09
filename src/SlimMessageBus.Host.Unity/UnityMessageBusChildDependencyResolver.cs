namespace SlimMessageBus.Host;

using Unity;

public class UnityMessageBusChildDependencyResolver : UnityMessageBusDependencyResolver, IChildDependencyResolver
{
    private readonly ILogger<UnityMessageBusChildDependencyResolver> logger;
    private bool _disposedValue;

    public IDependencyResolver Parent { get; }

    public UnityMessageBusChildDependencyResolver(IDependencyResolver parent, IUnityContainer container)
        : base(container)
    {
        logger = container.Resolve<ILogger<UnityMessageBusChildDependencyResolver>>();
        Parent = parent;
    }

    #region Dispose

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                logger.LogDebug("Disposing scope");
                Container.Dispose();
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion
}

