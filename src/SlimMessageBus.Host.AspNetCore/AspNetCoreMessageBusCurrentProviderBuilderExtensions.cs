using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AspNetCore
{
    /// <summary>
    /// Configuration extension to enable per-request scope <see cref="IMessageBus"/> dependency resolution for ASP.Net Core.
    /// see https://stackoverflow.com/a/40029302 
    /// see https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-2.1#request-services
    /// </summary>
    public static class AspNetCoreMessageBusCurrentProviderBuilderExtensions
    {
        public static MessageBusCurrentProviderBuilder FromPerRequestScope(this MessageBusCurrentProviderBuilder builder, IApplicationBuilder app)
        {
            // Set the MessageBus provider to be resolved from the request scope 
            // see https://stackoverflow.com/a/40029302 
            // see https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-2.1#request-services
            var httpContextAccessor = app.ApplicationServices.GetRequiredService<IHttpContextAccessor>();

            return builder.FromPerRequestScope(httpContextAccessor);
        }

        public static MessageBusCurrentProviderBuilder FromPerRequestScope(this MessageBusCurrentProviderBuilder builder, IHttpContextAccessor httpContextAccessor)
        {
            builder.SetProvider(() => httpContextAccessor.HttpContext.RequestServices.GetService<IMessageBus>());

            return builder;
        }
    }
}