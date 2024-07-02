namespace SlimMessageBus.Host.Outbox;

using Microsoft.Extensions.Logging;

using SlimMessageBus.Host.Outbox.Services;

public abstract class OutboxForwardingPublishInterceptor
{
}

/// <remarks>
/// Interceptor must be registered as a transient in order for outbox notifications to be raised.
/// Notifications are raised on disposal (if required), to ensure they occur outside of a transaction scope.
/// </remarks>
public sealed class OutboxForwardingPublishInterceptor<T>(
    ILogger<OutboxForwardingPublishInterceptor> logger,
    IOutboxRepository outboxRepository,
    IInstanceIdProvider instanceIdProvider,
    IOutboxNotificationService outboxNotificationService)
    : OutboxForwardingPublishInterceptor, IInterceptorWithOrder, IPublishInterceptor<T>, IDisposable where T : class
{
    static readonly internal string SkipOutboxHeader = "__SkipOutbox";

    private readonly ILogger _logger = logger;
    private readonly IOutboxRepository _outboxRepository = outboxRepository;
    private readonly IInstanceIdProvider _instanceIdProvider = instanceIdProvider;
    private readonly IOutboxNotificationService _outboxNotificationService = outboxNotificationService;

    private bool _notifyOutbox = false;

    public int Order => int.MaxValue;

    public void Dispose()
    {
        if (_notifyOutbox)
        {
            _outboxNotificationService.Notify();
        }

        GC.SuppressFinalize(this);
    }

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
            InstanceId = _instanceIdProvider.GetInstanceId()
        };
        await _outboxRepository.Save(outboxMessage, context.CancellationToken);
        
        // a message was sent, notify outbox service to poll on dispose (post transaction)
        _notifyOutbox = true;
    }
}
