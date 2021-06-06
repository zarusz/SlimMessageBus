namespace SlimMessageBus.Host.AspNetCore
{
    using Microsoft.AspNetCore.Builder;
    using SlimMessageBus.Host.DependencyResolver;
    using System;

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