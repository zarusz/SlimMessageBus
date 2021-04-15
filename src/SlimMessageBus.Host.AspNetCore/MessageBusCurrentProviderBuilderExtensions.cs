using Microsoft.AspNetCore.Builder;
using SlimMessageBus.Host.DependencyResolver;
using System;

namespace SlimMessageBus.Host.AspNetCore
{
    public static class MessageBusCurrentProviderBuilderExtensions
    {
        public static MessageBusCurrentProviderBuilder From(this MessageBusCurrentProviderBuilder builder, IApplicationBuilder applicationBuilder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            var dr = new AspNetCoreMessageBusDependencyResolver(applicationBuilder.ApplicationServices);
            return builder.From(dr);
        }
    }
}