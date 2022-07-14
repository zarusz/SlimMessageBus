namespace SlimMessageBus.Host.Memory
{
    using SlimMessageBus.Host.Config;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    public class MemoryMessageBusBuilder : MessageBusBuilder
    {
        internal MemoryMessageBusBuilder(MessageBusBuilder other) : base(other)
        {
        }

        public MemoryMessageBusBuilder AutoDeclareConsumers(IEnumerable<Assembly> assemblies)
        {
            var prospectTypes = assemblies.SelectMany(x => x.GetTypes())
                .Where(t => t.IsClass && !t.IsAbstract)
                .SelectMany(t => t.GetInterfaces(), (t, i) => new { Type = t, Interface = i })
                .Where(x => x.Interface.IsGenericType)
                .ToList();

            var producedMessageTypes = new HashSet<Type>();

            // register all consumers
            prospectTypes
                .Where(x => x.Interface.GetGenericTypeDefinition() == typeof(IConsumer<>))
                .Select(x => new { ConsumerType = x.Type, MessageType = x.Interface.GetGenericArguments()[0] })
                .ToList()
                .ForEach(find =>
                {
                    producedMessageTypes.Add(find.MessageType);
                    // register consumer
                    Consume(find.MessageType, x => x.Topic(x.MessageType.Name)
                        .WithConsumer(find.ConsumerType));
                });

            // register all handlers
            prospectTypes
                .Where(x => x.Interface.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
                .Select(x => new { HandlerType = x.Type, RequestType = x.Interface.GetGenericArguments()[0], ResponseType = x.Interface.GetGenericArguments()[1] })
                .ToList()
                .ForEach(find =>
                {
                    producedMessageTypes.Add(find.RequestType);
                    // register handler
                    Handle(find.RequestType, find.ResponseType, x => x.Topic(x.MessageType.Name)
                        .WithHandler(find.HandlerType));
                });

            // register all the producers
            foreach (var producedMessageType in producedMessageTypes)
            {
                Produce(producedMessageType, x => x.DefaultTopic(x.MessageType.Name));
            }

            return this;
        }

        public MemoryMessageBusBuilder AutoDeclareConsumers(params Assembly[] assemblies)
            => AutoDeclareConsumers(assemblies.AsEnumerable());

        public MemoryMessageBusBuilder AutoDeclareConsumers(Assembly assembly)
            => AutoDeclareConsumers(new[] { assembly });
    }
}
