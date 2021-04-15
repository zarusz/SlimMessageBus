using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SlimMessageBus.Host.DependencyResolver;
using SlimMessageBus.Host.MsDependencyInjection;

namespace SlimMessageBus.Host.AspNetCore
{
    /// <summary>
    /// <see cref="IDependencyResolver"/> implementation that resolves dependencies from the current ASP.NET Core web request (if present, otherwise falls back to the application root containser).
    /// </summary>
    public class AspNetCoreMessageBusDependencyResolver : MsDependencyInjectionDependencyResolver, IDependencyResolver
    {
        private readonly ILogger _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AspNetCoreMessageBusDependencyResolver(IServiceProvider serviceProvider, ILoggerFactory loggerFactory, IHttpContextAccessor httpContextAccessor)
            : base(serviceProvider, loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<AspNetCoreMessageBusDependencyResolver>();
            _httpContextAccessor = httpContextAccessor;
        }

        public AspNetCoreMessageBusDependencyResolver(IServiceProvider serviceProvider)
            : this(serviceProvider, serviceProvider.GetRequiredService<ILoggerFactory>(), serviceProvider.GetRequiredService<IHttpContextAccessor>())
        {
            // Set the MessageBus provider to be resolved from the request scope 
            // see https://stackoverflow.com/a/40029302 
            // see https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-2.1#request-services
        }

        public override object Resolve(Type type)
        {
            // When the call to resolve the given type is made within an HTTP Request, use the request scope service provider
            var httpContext = _httpContextAccessor?.HttpContext;
            if (httpContext != null)
            {
                _logger.LogDebug("The type {0} will be requested from the per-request scope", type);
                return httpContext.RequestServices.GetService(type);
            }

            // otherwise use the app wide scope provider
            _logger.LogDebug("The type {0} will be requested from the app scope", type);
            return base.Resolve(type);
        }
    }
}
