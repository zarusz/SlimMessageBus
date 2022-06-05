namespace SlimMessageBus.Host.Config
{
    using SlimMessageBus.Host.Interceptor;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    public class ReflectionDiscoveryScanner
    {
        private readonly IList<Assembly> assemblies;
        private IList<DiscoveryProspectType> prospectTypes = new List<DiscoveryProspectType>();

        IEnumerable<DiscoveryProspectType> ProspectTypes => prospectTypes;

        public static ReflectionDiscoveryScanner From(IEnumerable<Assembly> assemblies)
        {
            var scanner = new ReflectionDiscoveryScanner(assemblies);
            scanner.Scan();
            
            return scanner;
        }

        public ReflectionDiscoveryScanner(IEnumerable<Assembly> assemblies = null) 
            => this.assemblies = new List<Assembly>(assemblies ?? Enumerable.Empty<Assembly>());

        public void AddAssembly(Assembly assembly) => assemblies.Add(assembly);

        public void Scan()
        {
            prospectTypes = assemblies
                .SelectMany(x => x.GetTypes())
                .Where(t => t.IsClass && !t.IsAbstract)
                .SelectMany(t => t.GetInterfaces(), (t, i) => new DiscoveryProspectType { Type = t, InterfaceType = i })
                .ToList();
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
}