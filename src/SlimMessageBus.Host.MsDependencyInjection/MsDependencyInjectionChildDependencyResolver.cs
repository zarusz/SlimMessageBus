using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SlimMessageBus.Host.DependencyResolver;
using System;

namespace SlimMessageBus.Host.MsDependencyInjection
{
    /// <summary>
    /// An SMB DI adapter for the scope <see cref="IServiceScope"/>.
    /// </summary>
    public class MsDependencyInjectionChildDependencyResolver : IDependencyResolver
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<MsDependencyInjectionChildDependencyResolver> _logger;
        private readonly IServiceScope _serviceScope;
        private bool _disposedValue;

        public MsDependencyInjectionChildDependencyResolver(IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<MsDependencyInjectionChildDependencyResolver>();
            _serviceScope = serviceProvider.CreateScope();
        }

        /// <inheritdoc/>
        public IDependencyResolver CreateScope()
        {
            _logger.LogDebug("Creating child scope");
            return new MsDependencyInjectionChildDependencyResolver(_serviceScope.ServiceProvider, _loggerFactory);
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
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _logger.LogDebug("Disposing scope");
                    _serviceScope.Dispose();
                }

                _disposedValue = true;
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
}
