namespace SlimMessageBus.Host.Config;

public class ReflectionDiscoveryScanner
{
    private readonly IList<Assembly> _assemblies;
    private readonly Func<DiscoveryProspectType, bool> _filter;

    private IList<DiscoveryProspectType> prospectTypes = new List<DiscoveryProspectType>();

    public IEnumerable<DiscoveryProspectType> ProspectTypes => prospectTypes;

    public static ReflectionDiscoveryScanner From(IEnumerable<Assembly> assemblies, Func<DiscoveryProspectType, bool> filter = null)
        => new ReflectionDiscoveryScanner(assemblies, filter: filter).Scan();

    public static ReflectionDiscoveryScanner From(Assembly assembly, Func<DiscoveryProspectType, bool> filter = null)
        => new ReflectionDiscoveryScanner(filter: filter).AddAssembly(assembly).Scan();

    public ReflectionDiscoveryScanner(IEnumerable<Assembly> assemblies = null, Func<DiscoveryProspectType, bool> filter = null)
    {
        _assemblies = new List<Assembly>(assemblies ?? Enumerable.Empty<Assembly>());
        _filter = filter;
    }

    public ReflectionDiscoveryScanner AddAssembly(Assembly assembly)
    {
        _assemblies.Add(assembly);
        return this;
    }

    public ReflectionDiscoveryScanner Scan()
    {
        prospectTypes = _assemblies
            .SelectMany(x => x.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract)
            .SelectMany(t => t.GetInterfaces(), (t, i) => new DiscoveryProspectType { Type = t, InterfaceType = i })
            .Where(t => _filter == null || _filter(t))
            .ToList();

        return this;
    }

    public IReadOnlyCollection<DiscoveryConsumerType> GetConsumerTypes(Func<Type, bool> filterPredicate = null)
    {
        var foundTypes = ProspectTypes
            .Where(x => x.InterfaceType.IsGenericType && GenericTypesConsumers.Contains(x.InterfaceType.GetGenericTypeDefinition()))
            .Where(x => filterPredicate == null || filterPredicate(x.Type))
            .Select(x =>
            {
                var genericArguments = x.InterfaceType.GetGenericArguments();
                return new DiscoveryConsumerType
                {
                    ConsumerType = x.Type,
                    InterfaceType = x.InterfaceType,
                    MessageType = genericArguments[0],
                    ResponseType = genericArguments.Length > 1 ? genericArguments[1] : null
                };
            })
            .ToList();

        return foundTypes;
    }

    private static readonly Type[] GenericTypesConsumers = new[]
    {
        typeof(IConsumer<>),
        typeof(IRequestHandler<,>)
    };

    public IReadOnlyCollection<Type> GetMessageBusConfiguratorTypes()
    {
        var foundTypes = ProspectTypes
            .Where(x => x.InterfaceType == typeof(IMessageBusConfigurator))
            .Select(x => x.Type)
            .ToList();

        return foundTypes;
    }

    public IReadOnlyCollection<DiscoveryProspectType> GetInterceptorTypes()
    {
        var foundTypes = ProspectTypes
            .Where(x => x.InterfaceType.IsGenericType && GenericTypesInterceptors.Contains(x.InterfaceType.GetGenericTypeDefinition()))
            .ToList();

        return foundTypes;
    }

    private static readonly Type[] GenericTypesInterceptors = new[]
    {
        typeof(IProducerInterceptor<>),
        typeof(IConsumerInterceptor<>),

        typeof(IPublishInterceptor<>),

        typeof(ISendInterceptor<,>),
        typeof(IRequestHandlerInterceptor<,>)
    };
}