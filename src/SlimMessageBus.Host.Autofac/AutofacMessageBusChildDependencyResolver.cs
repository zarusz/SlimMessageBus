namespace SlimMessageBus.Host.Autofac
{
    using System;
    using global::Autofac;
    using Microsoft.Extensions.Logging;
    using SlimMessageBus.Host.DependencyResolver;

    public class AutofacMessageBusChildDependencyResolver : AutofacMessageBusDependencyResolver, IChildDependencyResolver
    {
        private readonly ILogger<AutofacMessageBusDependencyResolver> logger;
        private readonly ILifetimeScope lifetimeScope;
        private bool _disposedValue;

        public IDependencyResolver Parent { get; }

        public AutofacMessageBusChildDependencyResolver(IDependencyResolver parent, ILifetimeScope lifetimeScope, ILoggerFactory loggerFactory)
            : base(lifetimeScope, loggerFactory)
        {
            logger = loggerFactory.CreateLogger<AutofacMessageBusChildDependencyResolver>();
            this.lifetimeScope = lifetimeScope;
            Parent = parent;
        }

        public AutofacMessageBusChildDependencyResolver(IDependencyResolver parent, ILifetimeScope lifetimeScope)
            : this(parent, lifetimeScope, lifetimeScope.Resolve<ILoggerFactory>())
        {
        }

        #region Dispose

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    if (lifetimeScope != null)
                    {
                        logger.LogDebug("Disposing scope");
                        lifetimeScope.Dispose();
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
