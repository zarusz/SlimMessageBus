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
        }

        private static string DefaultMessageTypeToTopicConverter(Type type) => type.Name;

        /// <summary>
        /// In the specified assemblies, searches for any types that implement <see cref="IConsumer{TMessage}"/> or <see cref="IRequestHandler{TRequest, TResponse}"/>. 
        /// For every found type declares the produced/consumer and handler by applying the topic name that corresponds to the mesage name.
        /// </summary>
        /// <param name="assemblies"></param>
        /// <param name="messageTypeToTopicConverter">By default the type name is used for the topic name. This can be used to customize the topic name. For example, if have types that have same names but are in the namespaces, you might want to include the full type in the topic name.</param>
        /// <returns></returns>
        public MemoryMessageBusBuilder AutoDeclareFromConsumers(IEnumerable<Assembly> assemblies, Func<Type, string> messageTypeToTopicConverter = null, Func<Type, bool> consumerTypeFilter = null)
        {
            messageTypeToTopicConverter ??= DefaultMessageTypeToTopicConverter;

            var prospectTypes = assemblies.SelectMany(x => x.GetTypes())
                .Where(t => t.IsClass && !t.IsAbstract && (consumerTypeFilter == null || consumerTypeFilter(t)))
                .SelectMany(t => t.GetInterfaces(), (t, i) => new { Type = t, Interface = i })
                .Where(x => x.Interface.IsGenericType)
                .ToList();

            var producedMessageTypes = new HashSet<Type>();

            // register all consumers
            foreach (var find in prospectTypes
                .Where(x => x.Interface.GetGenericTypeDefinition() == typeof(IConsumer<>))
                .Select(x => new { ConsumerType = x.Type, MessageType = x.Interface.GetGenericArguments()[0] }))
            {
                producedMessageTypes.Add(find.MessageType);

                // register consumer
                var topicName = messageTypeToTopicConverter(find.MessageType);
                Consume(find.MessageType, x => x.Topic(topicName)
                    .WithConsumer(find.ConsumerType));
            }


            // register all handlers
            foreach (var find in prospectTypes
                .Where(x => x.Interface.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
                .Select(x => new { HandlerType = x.Type, RequestType = x.Interface.GetGenericArguments()[0], ResponseType = x.Interface.GetGenericArguments()[1] }))
            {
                producedMessageTypes.Add(find.RequestType);

                // register handler
                var topicName = messageTypeToTopicConverter(find.RequestType);
                Handle(find.RequestType, find.ResponseType, x => x.Topic(topicName)
                    .WithHandler(find.HandlerType));
            }

            // register all the producers
            foreach (var producedMessageType in producedMessageTypes)
            {
                var topicName = messageTypeToTopicConverter(producedMessageType);
                Produce(producedMessageType, x => x.DefaultTopic(topicName));
            }

            return this;
        }

        public MemoryMessageBusBuilder AutoDeclareFromConsumers(params Assembly[] assemblies)
            => AutoDeclareFromConsumers(assemblies.AsEnumerable());

        /// <summary>
        /// In the specified assemblies, searches for any types that implement <see cref="IConsumer{TMessage}"/> or <see cref="IRequestHandler{TRequest, TResponse}"/>. 
        /// For every found type declares the produced/consumer and handler by applying the topic name that corresponds to the mesage name.
        /// </summary>
        /// <param name="assemblies"></param>
        /// <param name="messageTypeToTopicConverter">By default the type name is used for the topic name. This can be used to customize the topic name. For example, if have types that have same names but are in the namespaces, you might want to include the full type in the topic name.</param>
        /// <returns></returns>
        public MemoryMessageBusBuilder AutoDeclareFromConsumers(Assembly assembly, Func<Type, string> messageTypeToTopicConverter = null, Func<Type, bool> consumerTypeFilter = null)
            => AutoDeclareFromConsumers(new[] { assembly }, messageTypeToTopicConverter, consumerTypeFilter);
    }
}
