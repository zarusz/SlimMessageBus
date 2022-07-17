namespace SlimMessageBus.Host.Memory
{
    using SlimMessageBus.Host.Config;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Message Bus Builder specific to the Memory transport.
    /// </summary>
    public class MemoryMessageBusBuilder : MessageBusBuilder
    {
        internal MemoryMessageBusBuilder(MessageBusBuilder other) : base(other)
        {
            // If the user did not setup RequestResponse settings, apply a defult setting with long TimeOut
            Settings.RequestResponse ??= new RequestResponseSettings
            {
                Timeout = TimeSpan.FromHours(1),
                Path = "responses"
            };
        }

        private static string DefaultMessageTypeToTopicConverter(Type type) => type.Name;

        /// <summary>
        /// In the specified assemblies, searches for any types that implement <see cref="IConsumer{TMessage}"/> or <see cref="IRequestHandler{TRequest, TResponse}"/>. 
        /// For every found type declares the produced and consumer/handler by applying the topic name that corresponds to the mesage name.
        /// </summary>
        /// <param name="assemblies"></param>
        /// <param name="consumerTypeFilter">Allows to apply a filter for any found consumer/handler.</param>
        /// <param name="messageTypeToTopicConverter">By default the type name is used for the topic name. This can be used to customize the topic name. For example, if have types that have same names but are in the namespaces, you might want to include the full type in the topic name.</param>
        /// <returns></returns>
        public MemoryMessageBusBuilder AutoDeclareFrom(IEnumerable<Assembly> assemblies, Func<Type, bool> consumerTypeFilter = null, Func<Type, string> messageTypeToTopicConverter = null)
        {
            messageTypeToTopicConverter ??= DefaultMessageTypeToTopicConverter;

            var prospectTypes = ReflectionDiscoveryScanner.From(assemblies).Scan().GetConsumerTypes(consumerTypeFilter);

            var producedMessageTypes = new HashSet<Type>();

            // register all consumers
            foreach (var find in prospectTypes.Where(x => x.InterfaceType.GetGenericTypeDefinition() == typeof(IConsumer<>)))
            {
                producedMessageTypes.Add(find.MessageType);

                // register consumer
                var topicName = messageTypeToTopicConverter(find.MessageType);
                Consume(find.MessageType, x => x.Topic(topicName)
                    .WithConsumer(find.ConsumerType));
            }

            // register all handlers
            foreach (var find in prospectTypes.Where(x => x.InterfaceType.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)))
            {
                producedMessageTypes.Add(find.MessageType);

                // register handler
                var topicName = messageTypeToTopicConverter(find.MessageType);
                Handle(find.MessageType, find.ResponseType, x => x.Topic(topicName)
                    .WithHandler(find.ConsumerType));
            }

            // register all the producers
            foreach (var producedMessageType in producedMessageTypes)
            {
                var topicName = messageTypeToTopicConverter(producedMessageType);
                Produce(producedMessageType, x => x.DefaultTopic(topicName));
            }

            return this;
        }

        public MemoryMessageBusBuilder AutoDeclareFrom(params Assembly[] assemblies)
            => AutoDeclareFrom(assemblies.AsEnumerable());

        /// <summary>
        /// In the specified assemblies, searches for any types that implement <see cref="IConsumer{TMessage}"/> or <see cref="IRequestHandler{TRequest, TResponse}"/>. 
        /// For every found type declares the produced and consumer/handler by applying the topic name that corresponds to the mesage name.
        /// </summary>
        /// <param name="assembly"></param>
        /// <param name="consumerTypeFilter">Allows to apply a filter for any found consumer/handler.</param>
        /// <param name="messageTypeToTopicConverter">By default the type name is used for the topic name. This can be used to customize the topic name. For example, if have types that have same names but are in the namespaces, you might want to include the full type in the topic name.</param>
        public MemoryMessageBusBuilder AutoDeclareFrom(Assembly assembly, Func<Type, bool> consumerTypeFilter = null, Func<Type, string> messageTypeToTopicConverter = null)
            => AutoDeclareFrom(new[] { assembly }, consumerTypeFilter, messageTypeToTopicConverter);
    }
}
