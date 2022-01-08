namespace SlimMessageBus.Host.AspNetCore
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using SlimMessageBus.Host.Config;
    using System;
    using System.Reflection;

    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers SlimMessageBus (<see cref="IMessageBus">) singleton instance and configures the ASP.NET Ms Dependency Injection for DI plugin.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configure"></param>
        /// <param name="loggerFactory">Use a custom logger factory. If not provided it will be obtained from the DI.</param>
        /// <param name="configureDependencyResolver">Confgure the DI plugin on the <see cref="MessageBusBuilder"/>. Default is true.</param>
        /// <param name="addConsumersFromAssembly">Specifies the list of assemblies to be searched for <see cref="IConsumer{TMessage}"/> or <see cref="IRequestHandler{TRequest, TResponse}"/> implementationss. The found types are added to the DI as Transient service.</param>
        /// <param name="addConfiguratorsFromAssembly">Specifies the list of assemblies to be searched for <see cref="IMessageBusConfigurator"/>. The found types are added to the DI as Transient service.</param>
        /// <returns></returns>
        public static IServiceCollection AddSlimMessageBus(
            this IServiceCollection services,
            Action<MessageBusBuilder, IServiceProvider> configure,
            ILoggerFactory loggerFactory = null,
            bool configureDependencyResolver = true,
            Assembly[] addConsumersFromAssembly = null,
            Assembly[] addConfiguratorsFromAssembly = null)
        {
            MsDependencyInjection.ServiceCollectionExtensions.AddSlimMessageBus(services, (mbb, services) =>
            {
                if (configureDependencyResolver)
                {
                    mbb.WithDependencyResolver(new AspNetCoreMessageBusDependencyResolver(services, loggerFactory));
                }
                configure(mbb, services);

            }, loggerFactory,
            configureDependencyResolver: false,
            addConsumersFromAssembly: addConsumersFromAssembly,
            addConfiguratorsFromAssembly: addConfiguratorsFromAssembly);

            return services;
        }
    }
}