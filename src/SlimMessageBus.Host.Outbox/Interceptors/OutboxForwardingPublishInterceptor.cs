namespace SlimMessageBus.Host.Outbox;

using Microsoft.Extensions.Logging;

public abstract class OutboxForwardingPublishInterceptor
{
}

public class OutboxForwardingPublishInterceptor<T>(
    ILogger<OutboxForwardingPublishInterceptor> logger,
    IOutboxRepository outboxRepository,
    IInstanceIdProvider instanceIdProvider,
    OutboxSettings outboxSettings)
    : OutboxForwardingPublishInterceptor, IPublishInterceptor<T> where T : class
{
    static readonly internal string SkipOutboxHeader = "__SkipOutbox";

    private readonly ILogger _logger = logger;
    private readonly IOutboxRepository _outboxRepository = outboxRepository;
    private readonly IInstanceIdProvider _instanceIdProvider = instanceIdProvider;
    private readonly OutboxSettings _outboxSettings = outboxSettings;

    public async Task OnHandle(T message, Func<Task> next, IProducerContext context)
    {
        var skipOutbox = context.Headers != null && context.Headers.ContainsKey(SkipOutboxHeader);
        var busMaster = context.GetMasterMessageBus();
        if (busMaster == null || skipOutbox)
        {
            if (skipOutbox)
            {
                context.Headers.Remove(SkipOutboxHeader);
            }

            // Do not use outbox for this message
            await next();
            return;
        }

        // Forward to outbox
        var messageType = message.GetType();

        _logger.LogDebug("Forwarding published message of type {MessageType} to the outbox", messageType.Name);

        // Take the proper serializer (meant for the bus)
        var messagePayload = busMaster.Serializer?.Serialize(messageType, message)
            ?? throw new PublishMessageBusException($"The {busMaster.Name} bus has no configured serializer, so it cannot be used with the outbox plugin");

        // Add message to the database, do not call next()
        var outboxMessage = new OutboxMessage
        {
            BusName = busMaster.Name,
            Headers = context.Headers,
            Path = context.Path,
            MessageType = messageType,
            MessagePayload = messagePayload,
            InstanceId = _instanceIdProvider.GetInstanceId(),
            LockInstanceId = _instanceIdProvider.GetInstanceId(),
            LockExpiresOn = DateTime.UtcNow.Add(_outboxSettings.LockExpiration)
        };
        await _outboxRepository.Save(outboxMessage, context.CancellationToken);
    }
}
