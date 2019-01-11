using System;
using System.Globalization;
using Common.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SlimMessageBus.Host.DependencyResolver;

namespace SlimMessageBus.Host.AspNetCore
{
    /// <summary>
    /// <see cref="IDependencyResolver"/> implementation that resolves dependencies from the current ASP.NET Core web request.
    /// </summary>
    public class AspNetCoreMessageBusDependencyResolver : IDependencyResolver
    {
        private static readonly ILog Log = LogManager.GetLogger<AspNetCoreMessageBusDependencyResolver>();

        private readonly IServiceProvider _serviceProvider;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AspNetCoreMessageBusDependencyResolver(IServiceProvider serviceProvider, IHttpContextAccessor httpContextAccessor)
        {
            _serviceProvider = serviceProvider;
            _httpContextAccessor = httpContextAccessor;
        }

        public AspNetCoreMessageBusDependencyResolver(IServiceProvider serviceProvider)
            : this(serviceProvider, serviceProvider.GetRequiredService<IHttpContextAccessor>())
        {
            // Set the MessageBus provider to be resolved from the request scope 
            // see https://stackoverflow.com/a/40029302 
            // see https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-2.1#request-services
        }

        #region Implementation of IDependencyResolver

        public object Resolve(Type type)
        {
            IServiceProvider currentServiceProvider;

            // When the call to resolve the given type is made within an HTTP Request, use the request scope service provider
            var httpContext = _httpContextAccessor?.HttpContext;
            if (httpContext != null)
            {
                Log.DebugFormat(CultureInfo.InvariantCulture, "The service {0} will be requested from the per-request scope", type);
                currentServiceProvider = httpContext.RequestServices;
            }
            else
            {
                // otherwise use the app wide scope provider
                Log.DebugFormat(CultureInfo.InvariantCulture, "The service {0} will be requested from the app scope", type);
                currentServiceProvider = _serviceProvider;
            }

            return currentServiceProvider.GetService(type);
        }

        #endregion
    }


}
