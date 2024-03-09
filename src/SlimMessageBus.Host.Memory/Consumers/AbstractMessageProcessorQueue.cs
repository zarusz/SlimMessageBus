namespace SlimMessageBus.Host.Memory;

public abstract class AbstractMessageProcessorQueue(IMessageProcessor<object> messageProcessor, ILogger logger) : IMessageProcessorQueue
{
    public abstract void Enqueue(object transportMessage, IReadOnlyDictionary<string, object> messageHeaders);

    protected async Task ProcessMessage(object transportMessage, IReadOnlyDictionary<string, object> messageHeaders, CancellationToken cancellationToken)
    {
        var consumerContextProperties = new Dictionary<string, object>
        {
            // Mark that the scope should be created for non blocking published messages
            [MemoryMessageBusProperties.CreateScope] = string.Empty
        };

        var r = await messageProcessor.ProcessMessage(
            transportMessage,
            messageHeaders: messageHeaders,
            consumerContextProperties: consumerContextProperties,
            currentServiceProvider: null,
            cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (r.Exception != null)
        {
            // We rely on the IMessageProcessor to execute the ConsumerErrorHandler<T>, but if it's not registered in the DI, it fails, or there is another fatal error then the message will be lost.
            logger.LogError(r.Exception, "Error processing message {Message} of type {MessageType}", transportMessage, transportMessage.GetType());
        }
    }
}
