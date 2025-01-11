namespace SlimMessageBus.Host.Memory;

public abstract partial class AbstractMessageProcessorQueue(IMessageProcessor<object> messageProcessor, ILogger logger) : IMessageProcessorQueue
{
    private readonly ILogger _logger = logger;

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
            LogMessageError(transportMessage, transportMessage.GetType(), r.Exception);
        }
    }

    #region Logging

    [LoggerMessage(
       EventId = 0,
       Level = LogLevel.Error,
       Message = "Error processing message {TransportMessage} of type {TransportMessageType}")]
    private partial void LogMessageError(object transportMessage, Type transportMessageType, Exception e);

    #endregion
}

#if NETSTANDARD2_0

public abstract partial class AbstractMessageProcessorQueue
{
    private partial void LogMessageError(object transportMessage, Type transportMessageType, Exception e)
        => _logger.LogError(e, "Error processing message {TransportMessage} of type {TransportMessageType}", transportMessage, transportMessageType);
}

#endif