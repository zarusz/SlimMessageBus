using Microsoft.AspNetCore.Builder;
using SlimMessageBus.Host.DependencyResolver;

namespace SlimMessageBus.Host.AspNetCore
{
    public static class MessageBusCurrentProviderBuilderExtensions
    {
        public static MessageBusCurrentProviderBuilder From(this MessageBusCurrentProviderBuilder builder, IApplicationBuilder applicationBuilder)
        {
            var dr = new AspNetCoreMessageBusDependencyResolver(applicationBuilder.ApplicationServices);
            return builder.From(dr);
        }
    }
}