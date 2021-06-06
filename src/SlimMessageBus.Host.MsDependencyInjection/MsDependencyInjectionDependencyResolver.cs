namespace SlimMessageBus.Host.MsDependencyInjection
{
    using Microsoft.Extensions.Logging;
    using SlimMessageBus.Host.DependencyResolver;
    using System;

    /// <summary>
    /// An SMB DI adapter for the <see cref="IServiceProvider"/>.
    /// </summary>
    public class MsDependencyInjectionDependencyResolver : IDependencyResolver
    {
        private bool _disposedValue;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<MsDependencyInjectionDependencyResolver> _logger;
        protected readonly IServiceProvider ServiceProvider;

        public MsDependencyInjectionDependencyResolver(IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<MsDependencyInjectionDependencyResolver>();
            ServiceProvider = serviceProvider;
        }

        public MsDependencyInjectionDependencyResolver(IServiceProvider serviceProvider)
            : this(serviceProvider, (ILoggerFactory)serviceProvider.GetService(typeof(ILoggerFactory)))
        {
        }

        /// <inheritdoc/>
        public virtual object Resolve(Type type)
        {
            _logger.LogDebug("Resolving type {0}", type);
            return ServiceProvider.GetService(type);
        }

        /// <inheritdoc/>
        public IDependencyResolver CreateScope()
        {
            _logger.LogDebug("Creating child scope");
            return new MsDependencyInjectionChildDependencyResolver(ServiceProvider, _loggerFactory);
        }

        #region Dispose

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // Note: The _serviceProvider is not owned by this instance it will be disposed externally.
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
