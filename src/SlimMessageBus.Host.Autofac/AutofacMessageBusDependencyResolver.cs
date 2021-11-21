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
        private readonly ILoggerFactory loggerFactory;
        private readonly ILogger<AutofacMessageBusDependencyResolver> logger;
        private readonly IComponentContext componentContext;
        private readonly ILifetimeScope lifetimeScope;

        public AutofacMessageBusDependencyResolver(IComponentContext container, ILoggerFactory loggerFactory)
        {
            this.loggerFactory = loggerFactory;
            logger = loggerFactory.CreateLogger<AutofacMessageBusDependencyResolver>();
            componentContext = container;
            lifetimeScope = container as ILifetimeScope;
        }

        public AutofacMessageBusDependencyResolver(IComponentContext container)
            : this(container, container.Resolve<ILoggerFactory>())
        {
        }

        public object Resolve(Type type)
        {
            logger.LogTrace("Resolving type {0}", type);
            var o = componentContext.Resolve(type);
            logger.LogTrace("Resolved type {0} to instance {1}", type, o);
            return o;
        }

        public IChildDependencyResolver CreateScope()
        {
            if (lifetimeScope == null) throw new InvalidOperationException($"The supplied Autofac {nameof(IComponentContext)} does not implement {nameof(ILifetimeScope)}");
            
            logger.LogDebug("Creating child scope");
            var scope = lifetimeScope.BeginLifetimeScope();
            return new AutofacMessageBusChildDependencyResolver(this, scope, loggerFactory);
        }
    }

}
