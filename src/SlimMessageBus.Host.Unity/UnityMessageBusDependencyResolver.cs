namespace SlimMessageBus.Host.Unity
{
    using System;
    using SlimMessageBus.Host.DependencyResolver;
    using global::Unity;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// An SMB DI adapter for the <see cref="IUnityContainer"/>.
    /// </summary>
    public class UnityMessageBusDependencyResolver : IDependencyResolver
    {
        private readonly ILogger<UnityMessageBusDependencyResolver> _logger;
        private readonly IUnityContainer _container;
        private bool _disposedValue;

        /// <summary>
        /// Indicated if the disposal of this instance should also dispose the associated <see cref="IUnityContainer"/>.
        /// </summary>
        protected bool DisposeScope { get; set; }

        public UnityMessageBusDependencyResolver(IUnityContainer container, ILogger<UnityMessageBusDependencyResolver> logger)
        {
            _logger = logger;
            _container = container;
        }

        public UnityMessageBusDependencyResolver(IUnityContainer container)
            : this(container, container.Resolve<ILogger<UnityMessageBusDependencyResolver>>())
        {
        }

        public object Resolve(Type type)
        {
            _logger.LogTrace("Resolving type {type}", type);
            var o = _container.Resolve(type);
            _logger.LogTrace("Resolved type {type} to instance {instance}", type, o);
            return o;
        }

        public IDependencyResolver CreateScope()
        {
            _logger.LogDebug("Creating child scope");
            var childContainer = _container.CreateChildContainer();
            return new UnityMessageBusDependencyResolver(childContainer, _logger) { DisposeScope = true };
        }

        #region Dispose

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    if (DisposeScope)
                    {
                        _logger.LogDebug("Disposing scope");
                        _container.Dispose();
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

        #endregion
    }
}
