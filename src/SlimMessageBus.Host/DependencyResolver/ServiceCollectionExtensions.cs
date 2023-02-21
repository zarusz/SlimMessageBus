namespace SlimMessageBus.Host;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using SlimMessageBus;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.Interceptor;

using System.Reflection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers SlimMessageBus (<see cref="IMessageBus">) singleton instance and configures the MsDependencyInjection as the DI plugin.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configure"></param>
    /// <param name="addConsumersFromAssembly">Specifies the list of assemblies to be searched for <see cref="IConsumer{TMessage}"/> or <see cref="IRequestHandler{TRequest, TResponse}"/> implementationss. The found types are added to the DI as Transient service.</param>
    /// <param name="addConfiguratorsFromAssembly">Specifies the list of assemblies to be searched for <see cref="IMessageBusConfigurator"/>. The found types are added to the DI as Transient service.</param>
    /// <param name="addInterceptorsFromAssembly">Specifies the list of assemblies to be searched for interceptors (<see cref="IPublishInterceptor{TMessage}"/>, <see cref="ISendInterceptor{TRequest, TResponse}"/>, <see cref="IConsumerInterceptor{TMessage}"/>, <see cref="IRequestHandler{TRequest, TResponse}"/>). The found types are added to the DI as Transient service.</param>
    /// <returns></returns>
    public static IServiceCollection AddSlimMessageBus(
        this IServiceCollection services,
        Action<MessageBusBuilder> configure,
        Assembly[] addConsumersFromAssembly = null,
        Assembly[] addConfiguratorsFromAssembly = null,
        Assembly[] addInterceptorsFromAssembly = null)
        => services.AddSlimMessageBus(
            configure: (mbb, svp) => configure(mbb),
            addConsumersFromAssembly: addConsumersFromAssembly,
            addConfiguratorsFromAssembly: addConfiguratorsFromAssembly,
            addInterceptorsFromAssembly: addInterceptorsFromAssembly);

    /// <summary>
    /// Registers SlimMessageBus (<see cref="IMessageBus">) singleton instance and configures the MsDependencyInjection as the DI plugin.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configure"></param>
    /// <param name="addConsumersFromAssembly">Specifies the list of assemblies to be searched for <see cref="IConsumer{TMessage}"/> or <see cref="IRequestHandler{TRequest, TResponse}"/> implementationss. The found types are added to the DI as Transient service.</param>
    /// <param name="addConfiguratorsFromAssembly">Specifies the list of assemblies to be searched for <see cref="IMessageBusConfigurator"/>. The found types are added to the DI as Transient service.</param>
    /// <param name="addInterceptorsFromAssembly">Specifies the list of assemblies to be searched for interceptors (<see cref="IPublishInterceptor{TMessage}"/>, <see cref="ISendInterceptor{TRequest, TResponse}"/>, <see cref="IConsumerInterceptor{TMessage}"/>, <see cref="IRequestHandler{TRequest, TResponse}"/>). The found types are added to the DI as Transient service.</param>
    /// <returns></returns>
    public static IServiceCollection AddSlimMessageBus(
        this IServiceCollection services,
        Action<MessageBusBuilder, IServiceProvider> configure,
        Assembly[] addConsumersFromAssembly = null,
        Assembly[] addConfiguratorsFromAssembly = null,
        Assembly[] addInterceptorsFromAssembly = null)
    {
        if (addConsumersFromAssembly != null)
        {
            services.AddMessageBusConsumersFromAssembly(addConsumersFromAssembly);
        }

        if (addConfiguratorsFromAssembly != null)
        {
            services.AddMessageBusConfiguratorsFromAssembly(addConfiguratorsFromAssembly);
        }

        if (addInterceptorsFromAssembly != null)
        {
            services.AddMessageBusInterceptorsFromAssembly(addInterceptorsFromAssembly);
        }

        // Single master bus that holds the defined consumers and message processing pipelines
        services.AddSingleton((svp) =>
        {
            var mbb = MessageBusBuilder.Create();
            mbb.WithDependencyResolver(svp);
            mbb.Configurators = svp.GetServices<IMessageBusConfigurator>();

            configure(mbb, svp);

            // Set the MessageBus.Current
            var currentBusProvider = svp.GetRequiredService<ICurrentMessageBusProvider>();
            MessageBus.SetProvider(currentBusProvider.GetCurrent);

            return (IMasterMessageBus)mbb.Build();
        });

        services.AddTransient<IConsumerControl>(svp => svp.GetRequiredService<IMasterMessageBus>());
        services.AddTransient<ITopologyControl>(svp => svp.GetRequiredService<IMasterMessageBus>());

        // Register transient message bus - this is a lightweight proxy that just introduces the current DI scope
        services.AddTransient(svp => new MessageBusProxy(svp.GetRequiredService<IMasterMessageBus>(), svp));

        services.AddTransient<IMessageBus>(svp => svp.GetRequiredService<MessageBusProxy>());
        services.AddTransient<IPublishBus>(svp => svp.GetRequiredService<MessageBusProxy>());
        services.AddTransient<IRequestResponseBus>(svp => svp.GetRequiredService<MessageBusProxy>());

        services.TryAddSingleton<ICurrentMessageBusProvider, CurrentMessageBusProvider>();

        services.AddHostedService<MessageBusHostedService>();

        return services;
    }

    /// <summary>
    /// Scans the specified assemblies (using reflection) for types that implement either <see cref="IConsumer{TMessage}"/> or <see cref="IRequestHandler{TRequest, TResponse}"/>. 
    /// The found types are registered in the DI as Transient service (both the consumer type and its interface are registered).
    /// </summary>
    /// <param name="services"></param>
    /// <param name="filterPredicate">Filtering predicate that allows to further narrow down the </param>
    /// <param name="assemblies">Assemblies to be scanned</param>
    /// <returns></returns>
    public static IServiceCollection AddMessageBusConsumersFromAssembly(this IServiceCollection services, Func<Type, bool> filterPredicate, params Assembly[] assemblies)
    {
        var foundTypes = ReflectionDiscoveryScanner.From(assemblies).GetConsumerTypes(filterPredicate);
        foreach (var (foundType, interfaceTypes) in foundTypes.GroupBy(x => x.ConsumerType, x => x.InterfaceType).ToDictionary(x => x.Key, x => x))
        {
            // Register the consumer/handler type
            services.TryAddTransient(foundType);

            foreach (var interfaceType in interfaceTypes)
            {
                // Register the interface of the consumer / handler
                services.TryAddTransient(interfaceType, foundType);
            }
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
    public static IServiceCollection AddMessageBusConsumersFromAssembly(this IServiceCollection services, params Assembly[] assemblies)
        => services.AddMessageBusConsumersFromAssembly(filterPredicate: null, assemblies);

    /// <summary>
    /// Scans the specified assemblies (using reflection) for types that implement <see cref="IMessageBusConfigurator{TMessage}"/> and adds them to DI.
    /// This types will be use during message bus configuration.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="assemblies">Assemblies to be scanned</param>
    /// <returns></returns>
    public static IServiceCollection AddMessageBusConfiguratorsFromAssembly(this IServiceCollection services, params Assembly[] assemblies)
    {
        var foundTypes = ReflectionDiscoveryScanner.From(assemblies).GetMessageBusConfiguratorTypes();
        foreach (var foundType in foundTypes)
        {
            services.AddTransient(typeof(IMessageBusConfigurator), foundType);
        }

        return services;
    }

    /// <summary>
    /// Scans the specified assemblies (using reflection) for types that implement one of the interceptor interfaces (<see cref="IPublishInterceptor{TMessage}"/> or <see cref="IConsumerInterceptor{TMessage}"/>) and adds them to DI.
    /// This types will be use during message bus configuration.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="assemblies">Assemblies to be scanned</param>
    /// <returns></returns>
    public static IServiceCollection AddMessageBusInterceptorsFromAssembly(this IServiceCollection services, params Assembly[] assemblies)
    {
        var foundTypes = ReflectionDiscoveryScanner.From(assemblies).GetInterceptorTypes();
        foreach (var foundType in foundTypes)
        {
            services.AddTransient(foundType.InterfaceType, foundType.Type);
        }

        return services;
    }
}
