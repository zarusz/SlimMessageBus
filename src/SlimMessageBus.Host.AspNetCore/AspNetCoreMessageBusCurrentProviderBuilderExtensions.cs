using System;
using Microsoft.AspNetCore.Builder;
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
        public static MessageBusCurrentProviderBuilder FromPerRequestScope(this MessageBusCurrentProviderBuilder builder, IServiceProvider serviceProvider)
        {
            var adapter = new AspNetCoreMessageBusDependencyResolver(serviceProvider);

            builder.SetProvider(() => (IMessageBus) adapter.Resolve(typeof(IMessageBus)));

            return builder;
        }
    }
}