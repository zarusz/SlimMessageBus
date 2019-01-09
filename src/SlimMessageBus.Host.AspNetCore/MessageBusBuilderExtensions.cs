using System;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AspNetCore
{
    public static class MessageBusBuilderExtensions
    {
        public static MessageBusBuilder WithDependencyResolverAsAspNetCore(this MessageBusBuilder builder, IServiceProvider serviceProvider)
        {
            return builder.WithDependencyResolver(new AspNetCoreMessageBusDependencyResolver(serviceProvider));
        }
    }
}