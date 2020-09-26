using System;
using Microsoft.Extensions.Logging;
using SlimMessageBus.Host.DependencyResolver;

namespace SlimMessageBus.Host.ServiceLocator
{
    public class ServiceLocatorMessageBusDependencyResolver : IDependencyResolver
    {
        private readonly ILogger _logger;

        public ServiceLocatorMessageBusDependencyResolver(ILogger logger)
        {
            _logger = logger;
        }

        #region Implementation of IDependencyResolver

        public object Resolve(Type type)
        {
            _logger.LogTrace("Resolving type {type}", type);
            var o = CommonServiceLocator.ServiceLocator.Current.GetInstance(type);
            _logger.LogTrace("Resolved type {type} to instance {instance}", type, o);
            return o;
        }

        #endregion
    }
}

