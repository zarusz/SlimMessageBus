namespace SlimMessageBus.Host
{
    using System;
    using SlimMessageBus.Host.DependencyResolver;
    using Unity;
    using SlimMessageBus.Host.Config;
    using System.Reflection;
    using System.Collections.Generic;

    public static class UnityContainerExtensions
    {
        /// <summary>
        /// Registers SlimMessageBus (<see cref="IMessageBus">) singleton instance and configures the MsDependencyInjection as the DI plugin.
        /// </summary>
        /// <param name="container"></param>
        /// <param name="configure"></param>
        /// <param name="addConsumersFromAssembly">Specifies the list of assemblies to be searched for <see cref="IConsumer{TMessage}"/> or <see cref="IRequestHandler{TRequest, TResponse}"/> implementationss. The found types are added to the DI as Transient service.</param>
        /// <param name="addConfiguratorsFromAssembly">Specifies the list of assemblies to be searched for <see cref="IMessageBusConfigurator"/>. The found types are added to the DI as Transient service.</param>
        /// <param name="addInterceptorsFromAssembly">Specifies the list of assemblies to be searched for interceptors (<see cref="IPublishInterceptor{TMessage}"/>, <see cref="ISendInterceptor{TRequest, TResponse}"/>, <see cref="IConsumerInterceptor{TMessage}"/>, <see cref="IRequestHandler{TRequest, TResponse}"/>). The found types are added to the DI as Transient service.</param>
        /// <returns></returns>
        public static IUnityContainer AddSlimMessageBus(
            this IUnityContainer container,
            Action<MessageBusBuilder, IUnityContainer> configure,
            Assembly[] addConsumersFromAssembly = null,
            Assembly[] addConfiguratorsFromAssembly = null,
            Assembly[] addInterceptorsFromAssembly = null)
        {
            if (addConsumersFromAssembly != null)
            {
                foreach (var foundType in ReflectionDiscoveryScanner.From(addConsumersFromAssembly).GetConsumerTypes())
                {
                    container.RegisterType(foundType.ConsumerType, TypeLifetime.Transient);
                }
            }

            if (addConfiguratorsFromAssembly != null)
            {
                var i = 0;
                foreach (var foundType in ReflectionDiscoveryScanner.From(addConfiguratorsFromAssembly).GetMessageBusConfiguratorTypes())
                {
                    // Note: Unity needs to have a unique name assigned to the type for that same interface implementation
                    container.RegisterType(typeof(IMessageBusConfigurator), foundType, $"Configurator_{i:00}", TypeLifetime.Transient);
                    i++;
                }
            }

            if (addInterceptorsFromAssembly != null)
            {
                var i = 0;
                foreach (var foundType in ReflectionDiscoveryScanner.From(addInterceptorsFromAssembly).GetInterceptorTypes())
                {
                    container.RegisterType(foundType.InterfaceType, foundType.Type, $"Interceptor_{i:00}", TypeLifetime.Transient);
                    i++;
                }
            }

            // Single master bus that holds the defined consumers and message processing pipelines
            container.RegisterFactory<IMasterMessageBus>((c) =>
            {
                var mbb = MessageBusBuilder.Create();
                mbb.WithDependencyResolver(c.Resolve<IDependencyResolver>());
                mbb.Configurators = c.Resolve<List<IMessageBusConfigurator>>();

                configure(mbb, c);

                return (IMasterMessageBus)mbb.Build();
            }, FactoryLifetime.Singleton);

            container.RegisterType<IDependencyResolver, UnityMessageBusDependencyResolver>(TypeLifetime.Hierarchical);

            container.RegisterFactory<IConsumerControl>(c => c.Resolve<IMasterMessageBus>(), FactoryLifetime.PerResolve);

            // Register transient message bus - this is a lightweight proxy that just introduces the current DI scope
            container.RegisterFactory<MessageBusProxy>(c => new MessageBusProxy(c.Resolve<IMasterMessageBus>(), c.Resolve<IDependencyResolver>()), FactoryLifetime.PerResolve);

            container.RegisterFactory<IMessageBus>(c => c.Resolve<MessageBusProxy>(), FactoryLifetime.PerResolve);
            container.RegisterFactory<IPublishBus>(c => c.Resolve<MessageBusProxy>(), FactoryLifetime.PerResolve);
            container.RegisterFactory<IRequestResponseBus>(c => c.Resolve<MessageBusProxy>(), FactoryLifetime.PerResolve);

            return container;
        }
    }
}

