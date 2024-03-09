namespace SlimMessageBus.Host.Memory;

public class MessageProcessorQueue(IMessageProcessor<object> messageProcessor, ILogger<MessageProcessorQueue> logger, CancellationToken cancellationToken) : AbstractMessageProcessorQueue(messageProcessor, logger)
{
    private readonly object _prevTaskLock = new();
    private Task _prevTask = null;

    public override void Enqueue(object transportMessage, IReadOnlyDictionary<string, object> messageHeaders)
    {
        lock (_prevTaskLock)
        {
            // Fire task, but do not wait here
            _prevTask = ProcessMessage(_prevTask, transportMessage, messageHeaders);
        }
    }

    private async Task ProcessMessage(Task prevTask, object transportMessage, IReadOnlyDictionary<string, object> messageHeaders)
    {
        // Ensure the previous message got processed first
        if (prevTask is not null)
        {
            await prevTask.ConfigureAwait(false);
        }
        // Then process this message
        await ProcessMessage(transportMessage, messageHeaders, cancellationToken).ConfigureAwait(false);
    }
}
