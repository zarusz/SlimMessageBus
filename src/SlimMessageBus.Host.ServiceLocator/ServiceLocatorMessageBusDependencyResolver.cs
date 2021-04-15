using System;
using Microsoft.Extensions.Logging;
using SlimMessageBus.Host.DependencyResolver;

namespace SlimMessageBus.Host.ServiceLocator
{
    /// <summary>
    /// An SMB DI adapter for the <see cref="CommonServiceLocator.ServiceLocator"/>.
    /// </summary>
    public class ServiceLocatorMessageBusDependencyResolver : IDependencyResolver
    {
        private readonly ILogger _logger;
        private bool disposedValue;

        public ServiceLocatorMessageBusDependencyResolver(ILogger logger)
        {
            _logger = logger;
        }

        public IDependencyResolver CreateScope()
            => this;
        
        public object Resolve(Type type)
        {
            _logger.LogTrace("Resolving type {type}", type);
            var o = CommonServiceLocator.ServiceLocator.Current.GetInstance(type);
            _logger.LogTrace("Resolved type {type} to instance {instance}", type, o);
            return o;
        }

        #region Dispose

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
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
}

