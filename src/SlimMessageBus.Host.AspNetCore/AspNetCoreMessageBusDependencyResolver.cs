namespace SlimMessageBus.Host.AspNetCore
{
    using System;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using SlimMessageBus.Host.DependencyResolver;
    using SlimMessageBus.Host.MsDependencyInjection;

    /// <summary>
    /// <see cref="IDependencyResolver"/> implementation that resolves dependencies from the current ASP.NET Core web request (if present, otherwise falls back to the application root containser).
    /// </summary>
    public class AspNetCoreMessageBusDependencyResolver : MsDependencyInjectionDependencyResolver, IDependencyResolver
    {
        private readonly ILogger logger;
        private readonly IHttpContextAccessor httpContextAccessor;

        public AspNetCoreMessageBusDependencyResolver(IServiceProvider serviceProvider, IHttpContextAccessor httpContextAccessor = null)
            : base(serviceProvider)
        {
            logger = serviceProvider.GetService<ILogger<AspNetCoreMessageBusDependencyResolver>>() ?? NullLogger<AspNetCoreMessageBusDependencyResolver>.Instance;
            this.httpContextAccessor = httpContextAccessor ?? serviceProvider.GetRequiredService<IHttpContextAccessor>();
        }

        public override object Resolve(Type type)
        {
            // When the call to resolve the given type is made within an HTTP Request, use the request scope service provider
            var httpContext = httpContextAccessor?.HttpContext;
            if (httpContext != null)
            {
                logger.LogDebug("The type {Type} will be requested from the per-request scope", type);
                return httpContext.RequestServices.GetService(type);
            }

            // otherwise use the app wide scope provider
            logger.LogDebug("The type {Type} will be requested from the app scope", type);
            return base.Resolve(type);
        }
    }
}
