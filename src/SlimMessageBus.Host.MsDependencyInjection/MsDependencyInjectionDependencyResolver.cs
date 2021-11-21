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
        protected readonly ILoggerFactory LoggerFactory;
        protected readonly IServiceProvider ServiceProvider;
        
        private readonly ILogger<MsDependencyInjectionDependencyResolver> logger;

        public MsDependencyInjectionDependencyResolver(IServiceProvider serviceProvider, ILoggerFactory loggerFactory = null)
        {
            LoggerFactory = loggerFactory ?? (ILoggerFactory)serviceProvider.GetService(typeof(ILoggerFactory));
            logger = LoggerFactory.CreateLogger<MsDependencyInjectionDependencyResolver>();
            ServiceProvider = serviceProvider;
        }

        /// <inheritdoc/>
        public virtual object Resolve(Type type)
        {
            logger.LogDebug("Resolving type {0}", type);
            return ServiceProvider.GetService(type);
        }

        /// <inheritdoc/>
        public IChildDependencyResolver CreateScope()
        {
            logger.LogDebug("Creating child scope");
            return new MsDependencyInjectionChildDependencyResolver(this, ServiceProvider, LoggerFactory);
        }
    }
}
