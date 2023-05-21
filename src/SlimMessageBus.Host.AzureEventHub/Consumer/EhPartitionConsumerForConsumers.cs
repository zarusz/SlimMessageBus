namespace SlimMessageBus.Host.AzureEventHub;

/// <summary>
/// <see cref="EhPartitionConsumer"/> implementation meant for processing messages coming to consumers (<see cref="IConsumer{TMessage}"/>) in pub-sub or handlers (<see cref="IRequestHandler{TRequest,TResponse}"/>) in request-response flows.
/// </summary>
public class EhPartitionConsumerForConsumers : EhPartitionConsumer
{
    private readonly IEnumerable<ConsumerSettings> _consumerSettings;

    public EhPartitionConsumerForConsumers(EventHubMessageBus messageBus, IEnumerable<ConsumerSettings> consumerSettings, GroupPathPartitionId pathGroupPartition)
        : base(messageBus, pathGroupPartition)
    {
        _consumerSettings = consumerSettings ?? throw new ArgumentNullException(nameof(consumerSettings));
        if (!consumerSettings.Any()) throw new ArgumentOutOfRangeException(nameof(consumerSettings));

        MessageProcessor = new MessageProcessor<EventData>(_consumerSettings, MessageBus, messageProvider: GetMessageFromTransportMessage, path: GroupPathPartition.ToString(), responseProducer: MessageBus, consumerContextInitializer: InitializeConsumerContext);
        CheckpointTrigger = CreateCheckpointTrigger();
    }

    protected ICheckpointTrigger CreateCheckpointTrigger()
    {
        var f = new CheckpointTriggerFactory(MessageBus.LoggerFactory, (configuredCheckpoints) => $"The checkpoint settings ({nameof(BuilderExtensions.CheckpointAfter)} and {nameof(BuilderExtensions.CheckpointEvery)}) across all the consumers that use the same Path {GroupPathPartition.Path} and Group {GroupPathPartition.Group} must be the same (found settings are: {string.Join(", ", configuredCheckpoints)})");
        return f.Create(_consumerSettings);
    }

    private object GetMessageFromTransportMessage(Type messageType, EventData e)
        => MessageBus.Serializer.Deserialize(messageType, e.Body.ToArray());

    private static void InitializeConsumerContext(EventData nativeMessage, ConsumerContext consumerContext)
        => consumerContext.SetTransportMessage(nativeMessage);
}