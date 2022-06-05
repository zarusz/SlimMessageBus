namespace SlimMessageBus.Host
{
    using System;
    using Autofac;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using SlimMessageBus.Host.DependencyResolver;

    /// <summary>
    /// An SMB DI adapter for the Autofac <see cref="ILifetimeScope"/>.
    /// </summary>
    public class AutofacMessageBusDependencyResolver : IDependencyResolver
    {
        private readonly ILogger<AutofacMessageBusDependencyResolver> logger;
        private readonly Func<IComponentContext> componentContextFunc;
        private readonly IComponentContext componentContext;

        public AutofacMessageBusDependencyResolver(Func<IComponentContext> componentContextFunc)
        {
            this.componentContextFunc = componentContextFunc;

            logger = componentContextFunc().ResolveOptional<ILogger<AutofacMessageBusDependencyResolver>>() ?? NullLogger<AutofacMessageBusDependencyResolver>.Instance;
        }

        protected AutofacMessageBusDependencyResolver(IComponentContext componentContext)
        {
            this.componentContext = componentContext;

            logger = componentContext.ResolveOptional<ILogger<AutofacMessageBusDependencyResolver>>() ?? NullLogger<AutofacMessageBusDependencyResolver>.Instance;
        }

        public object Resolve(Type type)
        {
            logger.LogTrace("Resolving type {0}", type);
            var o = ComponentContext.Resolve(type);
            logger.LogTrace("Resolved type {0} to instance {1}", type, o);
            return o;
        }

        public IChildDependencyResolver CreateScope()
        {
            if (!(ComponentContext is ILifetimeScope lifetimeScope))
            {
                throw new InvalidOperationException($"The supplied Autofac {nameof(IComponentContext)} does not implement {nameof(ILifetimeScope)}");
            }

            logger.LogDebug("Creating child scope");
            var scope = lifetimeScope.BeginLifetimeScope();
            return new AutofacMessageBusChildDependencyResolver(this, scope);
        }

        protected IComponentContext ComponentContext => componentContext ?? componentContextFunc();
    }
}
