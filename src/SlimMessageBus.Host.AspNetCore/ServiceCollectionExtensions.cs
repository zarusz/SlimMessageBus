namespace SlimMessageBus.Host.AspNetCore
{
    using Microsoft.Extensions.DependencyInjection;
    using SlimMessageBus.Host.Config;
    using SlimMessageBus.Host.DependencyResolver;
    using SlimMessageBus.Host.Interceptor;
    using System;
    using System.Reflection;

    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers SlimMessageBus (<see cref="IMessageBus">) singleton instance and configures the ASP.NET Ms Dependency Injection for DI plugin.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configure"></param>
        /// <param name="configureDependencyResolver">Confgure the DI plugin on the <see cref="MessageBusBuilder"/>. Default is true.</param>
        /// <param name="addConsumersFromAssembly">Specifies the list of assemblies to be searched for <see cref="IConsumer{TMessage}"/> or <see cref="IRequestHandler{TRequest, TResponse}"/> implementationss. The found types are added to the DI as Transient service.</param>
        /// <param name="addConfiguratorsFromAssembly">Specifies the list of assemblies to be searched for <see cref="IMessageBusConfigurator"/>. The found types are added to the DI as Transient service.</param>
        /// <param name="addInterceptorsFromAssembly">Specifies the list of assemblies to be searched for interceptors (<see cref="IPublishInterceptor{TMessage}"/>, <see cref="ISendInterceptor{TRequest, TResponse}"/>, <see cref="IConsumerInterceptor{TMessage}"/>, <see cref="IRequestHandler{TRequest, TResponse}"/>). The found types are added to the DI as Transient service.</param>
        /// <returns></returns>
        public static IServiceCollection AddSlimMessageBus(
            this IServiceCollection services,
            Action<MessageBusBuilder, IServiceProvider> configure,
            bool configureDependencyResolver = true,
            Assembly[] addConsumersFromAssembly = null,
            Assembly[] addConfiguratorsFromAssembly = null,
            Assembly[] addInterceptorsFromAssembly = null)
        {
            if (configureDependencyResolver)
            {
                // Provide an ASP.NET optimized IDependencyResolver
                services.AddTransient<IDependencyResolver, AspNetCoreMessageBusDependencyResolver>();
            }

            MsDependencyInjection.ServiceCollectionExtensions.AddSlimMessageBus(services,
                configure: configure,
                configureDependencyResolver: false,
                addConsumersFromAssembly: addConsumersFromAssembly,
                addConfiguratorsFromAssembly: addConfiguratorsFromAssembly,
                addInterceptorsFromAssembly: addInterceptorsFromAssembly);

            return services;
        }
    }
}