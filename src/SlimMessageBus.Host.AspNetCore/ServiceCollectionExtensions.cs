namespace SlimMessageBus.Host.AspNetCore
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using SlimMessageBus.Host.Config;
    using System;

    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers SlimMessageBus (<see cref="IMessageBus">) singleton instance and configures the ASP.NET Ms Dependency Injection for DI plugin.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configure"></param>
        /// <param name="loggerFactory">Use a custom logger factory. If not provided it will be obtained from the DI.</param>
        /// <returns></returns>
        public static IServiceCollection AddSlimMessageBus(this IServiceCollection services, Action<MessageBusBuilder, IServiceProvider> configure, ILoggerFactory loggerFactory = null, bool configureDependencyResolver = true)
        {
            MsDependencyInjection.ServiceCollectionExtensions.AddSlimMessageBus(services, (mbb, services) =>
            {
                if (configureDependencyResolver)
                {
                    mbb.WithDependencyResolver(new AspNetCoreMessageBusDependencyResolver(services, loggerFactory));
                }
                configure(mbb, services);

            }, loggerFactory, configureDependencyResolver: false);

            return services;
        }
    }
}