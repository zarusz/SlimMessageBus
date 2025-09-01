namespace SlimMessageBus.Host.Memory;

using System.Reflection;

/// <summary>
/// Message Bus Builder specific to the Memory transport.
/// </summary>
public class MemoryMessageBusBuilder : MessageBusBuilder
{
    internal MemoryMessageBusBuilder(MessageBusBuilder other) : base(other)
    {
        // If the user did not setup RequestResponse settings, apply a default setting with long TimeOut
        Settings.RequestResponse ??= new RequestResponseSettings
        {
            Timeout = TimeSpan.FromHours(1),
            Path = "responses"
        };
    }

    private static string DefaultMessageTypeToTopicConverter(Type type) => type.FullName;

    private static ISet<Type> GetAncestorTypes(Type messageType)
    {
        var ancestors = new HashSet<Type>();
        for (var mt = messageType; mt.BaseType != typeof(object) && mt.BaseType != null; mt = mt.BaseType)
        {
            ancestors.Add(mt.BaseType);
        }
        return ancestors;
    }

    /// <summary>
    /// Searches for any types that implement <see cref="IConsumer{TMessage}"/> or <see cref="IRequestHandler{TRequest, TResponse}"/> in the specified assemblies. 
    /// For every found type declares the produced and consumer/handler by applying the topic name that corresponds to the message name.
    /// </summary>
    /// <param name="assemblies"></param>
    /// <param name="consumerTypeFilter">Allows to apply a filter for the found consumer/handler types.</param>
    /// <param name="messageTypeToTopicConverter">By default the type name is used for the topic name. This can be used to customize the topic name. For example, if have types that have same names but are in the namespaces, you might want to include the full type in the topic name.</param>
    /// <param name="addServicesFromAssembly">If true, will add all the services from the assemblies to MSDI.</param>
    /// <returns></returns>
    public MemoryMessageBusBuilder AutoDeclareFrom(IEnumerable<Assembly> assemblies, Func<Type, bool> consumerTypeFilter = null, Func<Type, string> messageTypeToTopicConverter = null, bool addServicesFromAssembly = true)
    {
        messageTypeToTopicConverter ??= DefaultMessageTypeToTopicConverter;

        var prospectTypes = ReflectionDiscoveryScanner.From(assemblies).Scan()
            .GetConsumerTypes(consumerTypeFilter)
            // Take only closed generic types
            .Where(x => !x.ConsumerType.IsGenericType || x.ConsumerType.IsConstructedGenericType);

        var foundConsumers = prospectTypes.Where(x => x.InterfaceType.GetGenericTypeDefinition() == typeof(IConsumer<>)).ToList();
        var foundHandlers = prospectTypes.Where(x => x.InterfaceType.GetGenericTypeDefinition() == typeof(IRequestHandler<,>) || x.InterfaceType.GetGenericTypeDefinition() == typeof(IRequestHandler<>)).ToList();

        var knownMessageTypes = new HashSet<Type>();

        // register all consumers, handlers
        foreach (var consumer in foundConsumers.Concat(foundHandlers))
        {
            knownMessageTypes.Add(consumer.MessageType);
        }

        var ancestorsByType = new Dictionary<Type, ISet<Type>>();
        foreach (var messageType in knownMessageTypes)
        {
            ancestorsByType[messageType] = GetAncestorTypes(messageType);
        }

        var rootMessageTypes = new List<Type>();
        // register all the producers
        foreach (var messageType in knownMessageTypes)
        {
            var messageTypeAncestors = ancestorsByType[messageType];

            // register root message types only
            var isRoot = messageTypeAncestors.All(ancestor => !knownMessageTypes.Contains(ancestor));
            if (isRoot)
            {
                rootMessageTypes.Add(messageType);

                var topicName = messageTypeToTopicConverter(messageType);
                Produce(messageType, x => x.DefaultTopic(topicName));
            }
        }

        // register all consumers
        foreach (var rootMessageType in rootMessageTypes)
        {
            var consumers = foundConsumers.Where(x => x.MessageType == rootMessageType || ancestorsByType[x.MessageType].Contains(rootMessageType)).ToList();
            if (consumers.Count > 0)
            {
                var topicName = messageTypeToTopicConverter(rootMessageType);

                // register consumer
                Consume(rootMessageType, x =>
                {
                    x.Topic(topicName);
                    foreach (var consumer in consumers)
                    {
                        if (consumer.MessageType == rootMessageType && !x.ConsumerSettings.Invokers.Contains(x.ConsumerSettings))
                        {
                            x.WithConsumer(consumer.ConsumerType);
                        }
                        else
                        {
                            x.WithConsumer(derivedConsumerType: consumer.ConsumerType, derivedMessageType: consumer.MessageType);
                        }
                    }
                });
            }
        }

        // register all handlers
        foreach (var rootMessageType in rootMessageTypes)
        {
            var handlers = foundHandlers.Where(x => x.MessageType == rootMessageType || ancestorsByType[x.MessageType].Contains(rootMessageType)).ToList();
            if (handlers.Count > 0)
            {
                var topicName = messageTypeToTopicConverter(rootMessageType);

                var responseType = handlers.First(x => x.MessageType == rootMessageType).ResponseType;

                if (responseType == null)
                {
                    // register handler without a response
                    Handle(rootMessageType, x =>
                    {
                        x.Topic(topicName);
                        foreach (var handler in handlers)
                        {
                            if (handler.MessageType == rootMessageType && !x.ConsumerSettings.Invokers.Contains(x.ConsumerSettings))
                            {
                                x.WithHandler(handler.ConsumerType);
                            }
                            else
                            {
                                x.WithHandler(derivedHandlerType: handler.ConsumerType, derivedRequestType: handler.MessageType);
                            }
                        }
                    });
                }
                else
                {
                    // register handler
                    Handle(rootMessageType, responseType, x =>
                    {
                        x.Topic(topicName);
                        foreach (var handler in handlers)
                        {
                            if (handler.MessageType == rootMessageType && !x.ConsumerSettings.Invokers.Contains(x.ConsumerSettings))
                            {
                                x.WithHandler(handler.ConsumerType);
                            }
                            else
                            {
                                x.WithHandler(derivedHandlerType: handler.ConsumerType, derivedRequestType: handler.MessageType);
                            }
                        }
                    });
                }
            }
        }

        if (addServicesFromAssembly)
        {
            // Register all the services from the assemblies
            foreach (var assembly in assemblies)
            {
                this.AddServicesFromAssembly(assembly, filter: consumerTypeFilter);
            }
        }

        return this;
    }

    /// <summary>
    /// Searches for any types that implement <see cref="IConsumer{TMessage}"/>, <see cref="IRequestHandler{TRequest, TResponse}"/> or <see cref="IRequestHandler{TRequest}"/> in the assembly.
    /// For consumer type found will declares a producer and consumer/handler by applying the topic name that corresponds to the message name (provide custom converter to override this behavior).
    /// </summary>
    /// <param name="assembly"></param>
    /// <param name="consumerTypeFilter">Allows to apply a filter for any found consumer/handler.</param>
    /// <param name="messageTypeToTopicConverter">By default the type name is used for the topic name. This can be used to customize the topic name. For example, if have types that have same names but are in the namespaces, you might want to include the full type in the topic name.</param>
    /// <param name="addServicesFromAssembly">If true, will add all the services from the assemblies to MSDI.</param>
    public MemoryMessageBusBuilder AutoDeclareFrom(Assembly assembly, Func<Type, bool> consumerTypeFilter = null, Func<Type, string> messageTypeToTopicConverter = null, bool addServicesFromAssembly = true)
        => AutoDeclareFrom([assembly], consumerTypeFilter, messageTypeToTopicConverter, addServicesFromAssembly);

    /// <summary>
    /// Searches for any types that implement <see cref="IConsumer{TMessage}"/>, <see cref="IRequestHandler{TRequest, TResponse}"/> or <see cref="IRequestHandler{TRequest}"/> in the assembly.
    /// For consumer type found will declares a producer and consumer/handler by applying the topic name that corresponds to the message name (provide custom converter to override this behavior).
    /// </summary>
    public MemoryMessageBusBuilder AutoDeclareFrom(params Assembly[] assemblies)
        => AutoDeclareFrom(assemblies.AsEnumerable());

    /// <summary>
    /// Searches for any types that implement <see cref="IConsumer{TMessage}"/>, <see cref="IRequestHandler{TRequest, TResponse}"/> or <see cref="IRequestHandler{TRequest}"/> in the assembly of the specified type.
    /// For consumer type found will declares a producer and consumer/handler by applying the topic name that corresponds to the message name (provide custom converter to override this behavior).
    /// </summary>
    /// <param name="consumerTypeFilter">Allows to apply a filter for any found consumer/handler.</param>
    /// <param name="messageTypeToTopicConverter">By default the type name is used for the topic name. This can be used to customize the topic name. For example, if have types that have same names but are in the namespaces, you might want to include the full type in the topic name.</param>
    /// <param name="addServicesFromAssembly">If true, will add all the services from the assemblies to MSDI.</param>
    public MemoryMessageBusBuilder AutoDeclareFromAssemblyContaining<T>(Func<Type, bool> consumerTypeFilter = null, Func<Type, string> messageTypeToTopicConverter = null, bool addServicesFromAssembly = true)
        => AutoDeclareFrom([typeof(T).Assembly], consumerTypeFilter, messageTypeToTopicConverter, addServicesFromAssembly);
}
