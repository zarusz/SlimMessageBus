namespace SlimMessageBus.Host;

using System.Reflection;

using SlimMessageBus.Host.Consumer;
using SlimMessageBus.Host.Hybrid;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers SlimMessageBus (<see cref="IMessageBus">) master bus instance and required mbb. This can be called multiple times and the result will be additive.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configure">Action to configure the master (root) message bus</param>
    /// <returns></returns>
    public static IServiceCollection AddSlimMessageBus(this IServiceCollection services, Action<MessageBusBuilder> configure)
    {
        services.AddSlimMessageBus();

        if (configure is not null)
        {
            // Execute the mbb setup for services registration
            var mbb = (MessageBusBuilder)services.FirstOrDefault(x => x.ServiceType == typeof(MessageBusBuilder) && x.ImplementationInstance != null)?.ImplementationInstance;
            if (mbb is not null)
            {
                configure(mbb);

                // Execute post config actions for the master bus and its children
                foreach (var action in mbb.PostConfigurationActions.Concat(mbb.Children.Values.SelectMany(x => x.PostConfigurationActions)))
                {
                    action(services);
                }
            }
        }

        return services;
    }

    /// <summary>
    /// Registers SlimMessageBus (<see cref="IMessageBus">) master bus instance and required mbb. This can be called multiple times and the result will be additive.
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddSlimMessageBus(this IServiceCollection services)
    {
        // MessageBusBuilder
        if (!services.Any(x => x.ServiceType == typeof(MessageBusBuilder)))
        {
            // Register MessageBusBuilder for the root bus
            var mbb = MessageBusBuilder
                .Create()
                .WithProviderHybrid(); // By default apply the hybrid bus transport, the user can override.

            services.Add(ServiceDescriptor.Singleton(mbb));
        }

        // MessageBusSettings
        services.TryAddSingleton(svp =>
        {
            var mbb = svp.GetRequiredService<MessageBusBuilder>();
            mbb.WithDependencyResolver(svp);

            // Apply settings post processing
            foreach (var postProcessor in svp.GetServices<IMessageBusSettingsPostProcessor>())
            {
                postProcessor.Run(mbb.Settings);
            }

            return mbb.Settings;
        });

        // IMasterMessageBus - Single master bus that holds the defined consumers and message processing pipelines
        services.TryAddSingleton(svp =>
        {
            var mbb = svp.GetRequiredService<MessageBusBuilder>();
            var messageBusSettings = svp.GetRequiredService<MessageBusSettings>();

            // Set the MessageBus.Current
            var currentBusProvider = svp.GetRequiredService<ICurrentMessageBusProvider>();
            MessageBus.SetProvider(currentBusProvider.GetCurrent);

            return (IMasterMessageBus)mbb.Build();
        });

        services.TryAddTransient<IConsumerControl>(svp => svp.GetRequiredService<IMasterMessageBus>());
        services.TryAddTransient<ITopologyControl>(svp => svp.GetRequiredService<IMasterMessageBus>());

        // Register transient message bus - this is a lightweight proxy that just introduces the current DI scope
        services.TryAddTransient(svp => new MessageBusProxy(svp.GetRequiredService<IMasterMessageBus>(), svp));

        services.TryAddTransient<IMessageBus>(svp => svp.GetRequiredService<MessageBusProxy>());
        services.TryAddTransient<IPublishBus>(svp => svp.GetRequiredService<MessageBusProxy>());
        services.TryAddTransient<IRequestResponseBus>(svp => svp.GetRequiredService<MessageBusProxy>());

        services.TryAddSingleton<ICurrentMessageBusProvider, CurrentMessageBusProvider>();
        services.TryAddSingleton<IMessageTypeResolver, AssemblyQualifiedNameMessageTypeResolver>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMessageBusSettingsPostProcessor, ConsumerMethodPostProcessor>());

        services.TryAddSingleton<IMessageScopeAccessor, MessageScopeAccessor>();

        services.AddHostedService<MessageBusHostedService>();

        return services;
    }

    /// <summary>
    /// Scans the specified assemblies (using reflection) for types that implement consumer/handler interface, or interceptor interface).
    /// The found types are registered in the MSDI (both the consumer type and its interface are registered).
    /// </summary>
    /// <param name="mbb"></param>
    /// <param name="assembly"></param>
    /// <param name="filter">The filter to be applied for the found types - only types that evaluate the given filter predicate will be registered in MSDI.</param>
    /// <param name="consumerLifetime">The consumer lifetime under which the found types should be registered as.</param>
    /// <param name="interceptorLifetime">The interceptor lifetime under which the found types should be registered as.</param>
    /// <returns></returns>
    public static MessageBusBuilder AddServicesFromAssembly(
        this MessageBusBuilder mbb,
        Assembly assembly,
        Func<Type, bool> filter = null,
        ServiceLifetime consumerLifetime = ServiceLifetime.Transient,
        ServiceLifetime interceptorLifetime = ServiceLifetime.Transient)
    {
        var scan = ReflectionDiscoveryScanner.From(assembly);
        var foundConsumerTypes = scan.GetConsumerTypes(filter);
        var foundInterceptorTypes = scan.GetInterceptorTypes(filter);

        mbb.PostConfigurationActions.Add(services =>
        {
            foreach (var (foundType, interfaceTypes) in foundConsumerTypes.GroupBy(x => x.ConsumerType, x => x.InterfaceType).ToDictionary(x => x.Key, x => x))
            {
                // Register the consumer/handler type
                services.TryAdd(ServiceDescriptor.Describe(foundType, foundType, consumerLifetime));

                foreach (var interfaceType in interfaceTypes)
                {
                    if (foundType.IsGenericType && !foundType.IsConstructedGenericType)
                    {
                        // Skip open generic types
                        continue;
                    }

                    // Register the interface of the consumer / handler
                    services.TryAdd(ServiceDescriptor.Describe(interfaceType, svp => svp.GetRequiredService(foundType), consumerLifetime));
                }
            }

            foreach (var foundType in foundInterceptorTypes)
            {
                if (foundType.Type.IsGenericType && !foundType.Type.IsConstructedGenericType)
                {
                    // Skip open generic types
                    continue;
                }
                services.TryAddEnumerable(ServiceDescriptor.Describe(foundType.InterfaceType, foundType.Type, interceptorLifetime));
            }
        });
        return mbb;
    }

    /// <summary>
    /// Scans the specified assemblies (using reflection) for types that implement consumer/handler interface, or interceptor interface).
    /// The found types are registered in the MSDI (both the consumer type and its interface are registered).
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="mbb"></param>
    /// <param name="filter">The filter to be applied for the found types - only types that evaluate the given filter predicate will be registered in MSDI.</param>
    /// <param name="consumerLifetime">The consumer lifetime under which the found types should be registered as.</param>
    /// <param name="interceptorLifetime">The interceptor lifetime under which the found types should be registered as.</param>
    /// <returns></returns>
    public static MessageBusBuilder AddServicesFromAssemblyContaining<T>(
        this MessageBusBuilder mbb,
        Func<Type, bool> filter = null,
        ServiceLifetime consumerLifetime = ServiceLifetime.Transient,
        ServiceLifetime interceptorLifetime = ServiceLifetime.Transient) =>
        mbb.AddServicesFromAssembly(typeof(T).Assembly, filter, consumerLifetime: consumerLifetime, interceptorLifetime: interceptorLifetime);
}
