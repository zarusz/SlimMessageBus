namespace SlimMessageBus.Host.MsDependencyInjection
{
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
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

        public MsDependencyInjectionDependencyResolver(IServiceProvider serviceProvider)
        {
            LoggerFactory = (ILoggerFactory)serviceProvider.GetService(typeof(ILoggerFactory)) ?? NullLoggerFactory.Instance;
            logger = LoggerFactory.CreateLogger<MsDependencyInjectionDependencyResolver>();
            ServiceProvider = serviceProvider;
        }

        /// <inheritdoc/>
        public virtual object Resolve(Type type)
        {
            logger.LogDebug("Resolving type {Type}", type);
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
