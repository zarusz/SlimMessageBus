namespace SlimMessageBus.Host.AsyncApi;

using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

using Namotion.Reflection;

using NJsonSchema.Generation;

using Saunter;
using Saunter.AsyncApiSchema.v2;
using Saunter.Attributes;
using Saunter.Generation;
using Saunter.Generation.Filters;
using Saunter.Generation.SchemaGeneration;
using Saunter.Utils;

public record NamedServer(string Name, Server Server);

/// <summary>
/// <see cref="IDocumentGenerator"/> implementation for SlimMessageBus that populates the AsynAPI document from the SlimMessageBus configuration.
/// </summary>
public class MessageBusDocumentGenerator : IDocumentGenerator
{
    private readonly MessageBusSettings _busSettings;

    public MessageBusDocumentGenerator(MessageBusSettings busSettings)
    {
        _busSettings = busSettings;
    }

    public AsyncApiDocument GenerateDocument(TypeInfo[] asyncApiTypes, AsyncApiOptions options, AsyncApiDocument prototype, IServiceProvider serviceProvider)
    {
        var asyncApiSchema = prototype.Clone();

        var schemaResolver = new AsyncApiSchemaResolver(asyncApiSchema, options.SchemaOptions);

        var generator = new JsonSchemaGenerator(options.SchemaOptions);

        var busFilter = _busSettings.GetOrDefault<Func<MessageBusSettings, bool>?>(MessageBusBuilderExtensions.BusFilter, null);

        // ToDo: implement
        var serverByBusSettings = new Dictionary<MessageBusSettings, NamedServer>
        {
            { _busSettings, new("root", new Server("smb://smb.cluster", "kafka")) }
        };
        foreach (var childMessageBusSettings in _busSettings.Children)
        {
            if (busFilter == null || busFilter(childMessageBusSettings))
            {
                serverByBusSettings.Add(childMessageBusSettings, new(childMessageBusSettings.Name, new Server("smb://smb.cluster", "kafka")));
            }
        }

        foreach (var (messageBusSettings, namedServer) in serverByBusSettings)
        {
            asyncApiSchema.Servers.Add(namedServer.Name, namedServer.Server);

            using var scope = serviceProvider.CreateScope();
            asyncApiSchema.Channels = GenerateChannels(schemaResolver, options, generator, scope.ServiceProvider, messageBusSettings, namedServer);
        }

        var filterContext = new DocumentFilterContext(asyncApiTypes, schemaResolver, generator);
        foreach (var filterType in options.DocumentFilters)
        {
            var filter = (IDocumentFilter)serviceProvider.GetRequiredService(filterType);
            filter?.Apply(asyncApiSchema, filterContext);
        }

        return asyncApiSchema;
    }

    private static IDictionary<string, ChannelItem> GenerateChannels(AsyncApiSchemaResolver schemaResolver, AsyncApiOptions options, JsonSchemaGenerator jsonSchemaGenerator, IServiceProvider serviceProvider, MessageBusSettings messageBusSettings, NamedServer namedServer)
    {
        var channels = new Dictionary<string, ChannelItem>();

        GenerateChannelsFromConsumers(channels, schemaResolver, options, jsonSchemaGenerator, serviceProvider, messageBusSettings, namedServer);
        GenerateChannelsFromProducers(channels, schemaResolver, options, jsonSchemaGenerator, serviceProvider, messageBusSettings, namedServer);

        return channels;
    }

    /// <summary>
    /// Gets the SubscriptionName (Azure SB) / Consumer Group (Kafka/Azure EH) from the consumer settings.
    /// </summary>
    /// <param name="consumer"></param>
    /// <returns></returns>
    private static string TryGetSubscriptionName(ConsumerSettings consumer) =>
        consumer.GetOrDefault<string>("Group")
        ?? consumer.GetOrDefault<string>("SubscriptionName")
        ?? consumer.GetOrDefault<string>("Eh_Group");

    private static void GenerateChannelsFromConsumers(IDictionary<string, ChannelItem> channels, AsyncApiSchemaResolver schemaResolver, AsyncApiOptions options, JsonSchemaGenerator jsonSchemaGenerator, IServiceProvider serviceProvider, MessageBusSettings messageBusSettings, NamedServer namedServer)
    {
        foreach (var consumer in messageBusSettings.Consumers)
        {
            var subscribeOperation = GenerateOperationConsumer(consumer, schemaResolver, jsonSchemaGenerator, serviceProvider, namedServer);
            if (subscribeOperation == null)
            {
                continue;
            }

            var channelItem = new ChannelItem
            {
                Description = $"{consumer.PathKind}: {consumer.Path}",
                Subscribe = subscribeOperation,
                Servers = new List<string> { namedServer.Name },
            };

            var subsriptionName = TryGetSubscriptionName(consumer);
            var channelKey = subsriptionName != null
                    ? $"{consumer.Path}/{subsriptionName}"
                    : consumer.Path;

            channels.AddOrAppend(channelKey, channelItem);

            var context = new ChannelItemFilterContext(consumer.MessageType, schemaResolver, jsonSchemaGenerator, new ChannelAttribute(consumer.Path));
            foreach (var filterType in options.ChannelItemFilters)
            {
                var filter = (IChannelItemFilter)serviceProvider.GetRequiredService(filterType);
                filter.Apply(channelItem, context);
            }
        }
    }

    private static void GenerateChannelsFromProducers(IDictionary<string, ChannelItem> channels, AsyncApiSchemaResolver schemaResolver, AsyncApiOptions options, JsonSchemaGenerator jsonSchemaGenerator, IServiceProvider serviceProvider, MessageBusSettings messageBusSettings, NamedServer namedServer)
    {
        foreach (var producer in messageBusSettings.Producers)
        {
            var publishOperation = GenerateOperationProducer(producer, schemaResolver, jsonSchemaGenerator, serviceProvider, namedServer);
            if (publishOperation == null)
            {
                continue;
            }

            var channelItem = new ChannelItem
            {
                Description = $"{producer.PathKind}: {producer.DefaultPath}",
                Publish = publishOperation,
                Servers = new List<string> { namedServer.Name },
            };

            channels.AddOrAppend(producer.DefaultPath, channelItem);

            var context = new ChannelItemFilterContext(producer.MessageType, schemaResolver, jsonSchemaGenerator, new ChannelAttribute(producer.DefaultPath));
            foreach (var filterType in options.ChannelItemFilters)
            {
                var filter = (IChannelItemFilter)serviceProvider.GetRequiredService(filterType);
                filter.Apply(channelItem, context);
            }
        }
    }

    private static Operation? GenerateOperationConsumer(ConsumerSettings consumer, AsyncApiSchemaResolver schemaResolver, JsonSchemaGenerator jsonSchemaGenerator, IServiceProvider serviceProvider, NamedServer namedServer)
    {
        var consumerInstance = serviceProvider.GetRequiredService(consumer.ConsumerType);
        var consumerType = consumerInstance.GetType();
        var consumerHandleMethod = consumerType.GetRuntimeMethod(nameof(IConsumer<object>.OnHandle), new[] { consumer.MessageType });
        if (consumerHandleMethod == null)
        {
            return null;
        }

        var messages = new Messages();
        var operation = new Operation
        {
            OperationId = $"{consumer.Path}:{GetMessageId(consumer.MessageType)}",
            Summary = consumerHandleMethod!.GetXmlDocsSummary() ?? consumerType.GetXmlDocsSummary(),
            Description = (!string.IsNullOrEmpty(consumerHandleMethod!.GetXmlDocsRemarks()) ? consumerHandleMethod!.GetXmlDocsRemarks() : null) ?? (!string.IsNullOrEmpty(consumerType.GetXmlDocsRemarks()) ? consumerType.GetXmlDocsRemarks() : null),
            Message = messages,
        };

        // Add all message types pushed via this channel (topic/queue)
        foreach (var invoker in consumer.Invokers)
        {
            var message = GenerateMessageFromAttribute(invoker.MessageType, schemaResolver, jsonSchemaGenerator, namedServer);
            if (message != null)
            {
                messages.OneOf.Add(message);
            }
        }

        if (messages.OneOf.Count == 1)
        {
            operation.Message = messages.OneOf.First();
        }

        return operation;
    }

    internal static string GetMessageId(Type messageType)
    {
        if (messageType.IsGenericType)
        {
            var paramTypeNames = string.Join("_", messageType.GetGenericArguments().Select(x => x.Name));
            return $"{messageType.GetGenericTypeDefinition().Name}[[{paramTypeNames}]]";
        }
        return messageType.Name;
    }

    private static Operation? GenerateOperationProducer(ProducerSettings producer, AsyncApiSchemaResolver schemaResolver, JsonSchemaGenerator jsonSchemaGenerator, IServiceProvider serviceProvider, NamedServer namedServer)
    {
        var messages = new Messages();
        var operation = new Operation
        {
            OperationId = $"{producer.DefaultPath}_{producer.MessageType.Name}",
            Message = messages,
        };

        // Add all message types pushed via this channel (topic/queue)
        var message = GenerateMessageFromAttribute(producer.MessageType, schemaResolver, jsonSchemaGenerator, namedServer);
        if (message != null)
        {
            messages.OneOf.Add(message);
        }

        if (messages.OneOf.Count == 1)
        {
            operation.Message = messages.OneOf.First();
        }

        return operation;
    }

    private static IMessage GenerateMessageFromAttribute(Type messageType, AsyncApiSchemaResolver schemaResolver, JsonSchemaGenerator jsonSchemaGenerator, NamedServer namedServer)
    {
        var message = new Message
        {
            MessageId = GetMessageId(messageType),
            Payload = jsonSchemaGenerator.Generate(messageType, schemaResolver),
            Title = messageType.Name,
            Summary = messageType.GetXmlDocsSummary(),
            Description = messageType.GetXmlDocsRemarks(),
        };
        message.Name = message.Payload.ActualSchema.Id;

        return schemaResolver.GetMessageOrReference(message);
    }

}
