namespace SlimMessageBus.Host.ServiceLocator
{
    using System;
    using Microsoft.Extensions.Logging;
    using SlimMessageBus.Host.DependencyResolver;

    /// <summary>
    /// An SMB DI adapter for the <see cref="CommonServiceLocator.ServiceLocator"/>.
    /// </summary>
    public class ServiceLocatorMessageBusDependencyResolver : IDependencyResolver
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly ILogger logger;

        public ServiceLocatorMessageBusDependencyResolver(ILoggerFactory loggerFactory)
        {
            this.loggerFactory = loggerFactory;
            logger = loggerFactory.CreateLogger<ServiceLocatorMessageBusDependencyResolver>();
        }

        public IChildDependencyResolver CreateScope()
            => new ServiceLocatorMessageBusChildDependencyResolver(this, loggerFactory);

        public object Resolve(Type type)
        {
            logger.LogTrace("Resolving type {type}", type);
            var o = CommonServiceLocator.ServiceLocator.Current.GetInstance(type);
            logger.LogTrace("Resolved type {type} to instance {instance}", type, o);
            return o;
        }

        private class ServiceLocatorMessageBusChildDependencyResolver : ServiceLocatorMessageBusDependencyResolver, IChildDependencyResolver
        {
            public IDependencyResolver Parent { get; }

            public ServiceLocatorMessageBusChildDependencyResolver(IDependencyResolver parent, ILoggerFactory loggerFactory)
                : base(loggerFactory)
            {
                Parent = parent;
            }

            public void Dispose()
            {
                // Do Nothing
            }
        }
    }
}

