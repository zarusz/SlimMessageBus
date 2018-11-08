using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AspNetCore
{
    public static class MessageBusBuilderExtensions
    {
        public static MessageBusBuilder WithDependencyResolverAsAspNetCore(this MessageBusBuilder builder, IApplicationBuilder app)
        {
            var httpContextAccessor = app.ApplicationServices.GetRequiredService<IHttpContextAccessor>();

            return builder.WithDependencyResolverAsAspNetCore(httpContextAccessor);
        }

        public static MessageBusBuilder WithDependencyResolverAsAspNetCore(this MessageBusBuilder builder, IHttpContextAccessor httpContextAccessor)
        {
            return builder.WithDependencyResolver(new AspNetCoreMessageBusDependencyResolver(httpContextAccessor));
        }
    }
}