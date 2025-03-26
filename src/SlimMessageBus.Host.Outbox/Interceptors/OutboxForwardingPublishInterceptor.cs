namespace SlimMessageBus.Host.Outbox;

public abstract class OutboxForwardingPublishInterceptor
{
}

/// <remarks>
/// Interceptor must be registered as a transient in order for outbox notifications to be raised.
/// Notifications are raised on disposal (if required), to ensure they occur outside of a transaction scope.
/// </remarks>
public sealed class OutboxForwardingPublishInterceptor<T>(
    ILogger<OutboxForwardingPublishInterceptor> logger,
    IOutboxMessageFactory outboxMessageFactory,
    IOutboxNotificationService outboxNotificationService,
    OutboxSettings outboxSettings)
    : OutboxForwardingPublishInterceptor, IInterceptorWithOrder, IPublishInterceptor<T>, IDisposable
    where T : class
{
    internal const string SkipOutboxHeader = "__SkipOutbox";

    private bool _notifyOutbox = false;

    public int Order => int.MaxValue;

    public void Dispose()
    {
        if (_notifyOutbox)
        {
            outboxNotificationService.Notify();
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

        // Forward to outbox - do not call next()

        var messageType = message.GetType();
        // Take the proper serializer (meant for the bus)
        var messagePayload = busMaster.SerializerProvider?.GetSerializer(context.Path).Serialize(messageType, message)
                ?? throw new PublishMessageBusException($"The {busMaster.Name} bus has no configured serializer, so it cannot be used with the outbox plugin");

        var outboxMessage = await outboxMessageFactory.Create(
            busName: busMaster.Name,
            headers: context.Headers,
            path: context.Path,
            messageType: outboxSettings.MessageTypeResolver.ToName(messageType),
            messagePayload: messagePayload,
            cancellationToken: context.CancellationToken
        );

        logger.LogDebug("Forwarding published message of type {MessageType} to the outbox with Id {OutboxMessageId}", messageType.Name, outboxMessage);

        // A message was sent, notify outbox service to poll on dispose (post transaction)
        _notifyOutbox = true;
    }
}
