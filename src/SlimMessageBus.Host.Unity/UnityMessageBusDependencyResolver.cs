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
        private readonly ILoggerFactory loggerFactory;
        private readonly ILogger<UnityMessageBusDependencyResolver> logger;
        protected IUnityContainer Container { get; }

        public UnityMessageBusDependencyResolver(IUnityContainer container, ILoggerFactory loggerFactory)
        {
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger<UnityMessageBusDependencyResolver>();
            Container = container;
        }

        public UnityMessageBusDependencyResolver(IUnityContainer container)
            : this(container, container.Resolve<ILoggerFactory>())
        {
        }

        public object Resolve(Type type)
        {
            logger.LogTrace("Resolving type {type}", type);
            var o = Container.Resolve(type);
            logger.LogTrace("Resolved type {type} to instance {instance}", type, o);
            return o;
        }

        public IChildDependencyResolver CreateScope()
        {
            logger.LogDebug("Creating child scope");
            var childContainer = Container.CreateChildContainer();
            return new UnityMessageBusChildDependencyResolver(this, childContainer, loggerFactory);
        }
    }
}

