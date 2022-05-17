namespace SlimMessageBus.Host
{
    using System;
    using SlimMessageBus.Host.DependencyResolver;
    using Unity;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// An SMB DI adapter for the <see cref="IUnityContainer"/>.
    /// </summary>
    public class UnityMessageBusDependencyResolver : IDependencyResolver
    {
        private readonly ILogger<UnityMessageBusDependencyResolver> logger;
        protected IUnityContainer Container { get; }

        public UnityMessageBusDependencyResolver(IUnityContainer container)
        {
            logger = container.Resolve<ILogger<UnityMessageBusDependencyResolver>>();
            Container = container;
        }

        public object Resolve(Type type)
        {
            logger.LogTrace("Resolving type {Type}", type);
            var o = Container.Resolve(type);
            logger.LogTrace("Resolved type {Type} to instance {Instance}", type, o);
            return o;
        }

        public IChildDependencyResolver CreateScope()
        {
            logger.LogDebug("Creating child scope");
            var childContainer = Container.CreateChildContainer();
            return new UnityMessageBusChildDependencyResolver(this, childContainer);
        }
    }
}

