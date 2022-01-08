namespace SlimMessageBus.Host.MsDependencyInjection
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using SlimMessageBus.Host.Config;
    using System;
    using System.Linq;
    using System.Reflection;

    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers SlimMessageBus (<see cref="IMessageBus">) singleton instance and configures the MsDependencyInjection as the DI plugin.
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
            if (addConsumersFromAssembly != null)
            {
                services.AddMessageBusConsumersFromAssembly(filterPredicate: null, addConsumersFromAssembly);
            }

            if (addConfiguratorsFromAssembly != null)
            {
                services.AddMessageBusConfiguratorsFromAssembly(addConfiguratorsFromAssembly);
            }

            services.AddSingleton((svp) =>
            {
                var configurators = svp.GetServices<IMessageBusConfigurator>();

                var mbb = MessageBusBuilder.Create();
                if (configureDependencyResolver)
                {
                    mbb.WithDependencyResolver(new MsDependencyInjectionDependencyResolver(svp, loggerFactory));
                }

                foreach (var configurator in configurators)
                {
                    configurator.Configure(mbb, "default");
                }

                configure(mbb, svp);

                return mbb.Build();
            });

            services.AddTransient<IPublishBus>(svp => svp.GetRequiredService<IMessageBus>());
            services.AddTransient<IRequestResponseBus>(svp => svp.GetRequiredService<IMessageBus>());
            services.AddTransient<IConsumerControl>(svp => (IConsumerControl)svp.GetRequiredService<IMessageBus>());

            return services;
        }

        /// <summary>
        /// Scans the specified assemblies (using reflection) for types that implement either <see cref="IConsumer{TMessage}"/> or <see cref="IRequestHandler{TRequest, TResponse}"/>. 
        /// The found types are registered in the DI as Transient service.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="filterPredicate">Filtering predicate that allows to further narrow down the </param>
        /// <param name="assemblies">Assemblies to be scanned</param>
        /// <returns></returns>
        public static IServiceCollection AddMessageBusConsumersFromAssembly(
            this IServiceCollection services,
            Func<Type, bool> filterPredicate,
            params Assembly[] assemblies)
        {
            var foundTypes = assemblies
                .SelectMany(x => x.GetTypes())
                .Where(t => t.IsClass && !t.IsAbstract && t.IsVisible)
                .SelectMany(t => t.GetInterfaces(), (t, i) => new { Type = t, Interface = i })
                .Where(x => x.Interface.IsGenericType && (x.Interface.GetGenericTypeDefinition() == typeof(IConsumer<>) || x.Interface.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)))
                .Where(x => filterPredicate == null || filterPredicate(x.Type))
                .Select(x =>
                {
                    var genericArguments = x.Interface.GetGenericArguments();
                    return new
                    {
                        ConsumerType = x.Type,
                        MessageType = genericArguments[0],
                        ResponseType = genericArguments.Length > 1 ? genericArguments[1] : null
                    };
                })
                .ToList();

            foreach (var foundType in foundTypes)
            {
                services.AddTransient(foundType.ConsumerType);
            }

            return services;
        }

        /// <summary>
        /// Scans the specified assemblies (using reflection) for types that implement either <see cref="IConsumer{TMessage}"/> or <see cref="IRequestHandler{TRequest, TResponse}"/>. 
        /// The found types are registered in the DI as Transient service.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="assemblies">Assemblies to be scanned</param>
        /// <returns></returns>
        public static IServiceCollection AddMessageBusConsumersFromAssembly(
            this IServiceCollection services,
            params Assembly[] assemblies)
            => services.AddMessageBusConsumersFromAssembly(filterPredicate: null, assemblies);

        /// <summary>
        /// Scans the specified assemblies (using reflection) for types that implement <see cref="IMessageBusConfigurator{TMessage}"/> and adds them to DI.
        /// This types will be use during message bus configuration.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="assemblies">Assemblies to be scanned</param>
        /// <returns></returns>
        public static IServiceCollection AddMessageBusConfiguratorsFromAssembly(
            this IServiceCollection services,
            params Assembly[] assemblies)
        {
            var foundTypes = assemblies
            .SelectMany(x => x.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract && t.IsVisible)
            .SelectMany(t => t.GetInterfaces(), (t, i) => new { Type = t, Interface = i })
            .Where(x => x.Interface == typeof(IMessageBusConfigurator))
            .Select(x => x.Type)
            .ToList();

            foreach (var foundType in foundTypes)
            {
                services.AddTransient(typeof(IMessageBusConfigurator), foundType);
            }

            return services;
        }
    }
}
