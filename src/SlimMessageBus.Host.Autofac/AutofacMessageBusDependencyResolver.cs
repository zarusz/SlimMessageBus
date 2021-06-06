namespace SlimMessageBus.Host.Autofac
{
    using System;
    using global::Autofac;
    using Microsoft.Extensions.Logging;
    using SlimMessageBus.Host.DependencyResolver;

    /// <summary>
    /// An SMB DI adapter for the Autofac <see cref="ILifetimeScope"/>.
    /// </summary>
    public class AutofacMessageBusDependencyResolver : IDependencyResolver
    {
        private readonly ILogger<AutofacMessageBusDependencyResolver> _logger;
        private readonly IComponentContext _componentContext;
        private readonly ILifetimeScope _lifetimeScope;
        private bool _disposedValue;

        /// <summary>
        /// Indicated if the disposal of this instance should also dispose the associated <see cref="ILifetimeScope"/>.
        /// </summary>
        protected bool DisposeScope { get; set; }

        public AutofacMessageBusDependencyResolver(IComponentContext container, ILogger<AutofacMessageBusDependencyResolver> logger)
        {
            _logger = logger;
            _componentContext = container;
            _lifetimeScope = container as ILifetimeScope;
        }

        public AutofacMessageBusDependencyResolver(IComponentContext container)
            : this(container, container.Resolve<ILogger<AutofacMessageBusDependencyResolver>>())
        {
        }

        public object Resolve(Type type)
        {
            _logger.LogTrace("Resolving type {0}", type);
            var o = _componentContext.Resolve(type);
            _logger.LogTrace("Resolved type {0} to instance {1}", type, o);
            return o;
        }

        public IDependencyResolver CreateScope()
        {
            if (_lifetimeScope == null) throw new InvalidOperationException($"The supplied Autofac {nameof(IComponentContext)} does not implement {nameof(ILifetimeScope)}");
            
            _logger.LogDebug("Creating child scope");
            var scope = _lifetimeScope.BeginLifetimeScope();
            return new AutofacMessageBusDependencyResolver(scope, _logger) { DisposeScope = true };
        }

        #region Dispose

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    if (DisposeScope && _lifetimeScope != null)
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

        #endregion
    }
}
