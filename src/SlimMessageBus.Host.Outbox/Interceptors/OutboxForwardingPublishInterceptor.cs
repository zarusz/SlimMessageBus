namespace SlimMessageBus.Host.Outbox;

using Microsoft.Extensions.Logging;

using SlimMessageBus;
using SlimMessageBus.Host.Interceptor;

public abstract class OutboxForwardingPublishInterceptor
{
}

public class OutboxForwardingPublishInterceptor<T> : OutboxForwardingPublishInterceptor, IPublishInterceptor<T> where T : class
{
    static readonly internal string SkipOutboxHeader = "__SkipOutbox";

    private readonly ILogger _logger;
    private readonly IOutboxRepository _outboxRepository;
    private readonly IInstanceIdProvider _instanceIdProvider;
    private readonly OutboxSettings _outboxSettings;

    public OutboxForwardingPublishInterceptor(ILogger<OutboxForwardingPublishInterceptor> logger, IOutboxRepository outboxRepository, IInstanceIdProvider instanceIdProvider, OutboxSettings outboxSettings)
    {
        _logger = logger;
        _outboxRepository = outboxRepository;
        _instanceIdProvider = instanceIdProvider;
        _outboxSettings = outboxSettings;
    }

    private bool IsOutboxEnabled(IProducerContext context, out MessageBusBase bus)
    {
        bus = context.Bus as MessageBusBase;

        if (bus is null || context is not ProducerContext producerContext)
        {
            return false;
        }

        // If producer has outbox enabled, if not set check if bus has outbox enabled
        var outboxEnabled = producerContext.ProducerSettings.GetOrDefault<bool?>(BuilderExtensions.PropertyOutboxEnabled, null)
            ?? bus.Settings.GetOrDefault(BuilderExtensions.PropertyOutboxEnabled, false);

        return outboxEnabled;
    }

    public async Task OnHandle(T message, Func<Task> next, IProducerContext context)
    {
        var outboxEnabled = IsOutboxEnabled(context, out var bus);
        var skipOutbox = context.Headers != null && context.Headers.ContainsKey(SkipOutboxHeader);
        if (!outboxEnabled || skipOutbox)
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
        var messagePayload = bus.Serializer?.Serialize(messageType, message)
            ?? throw new PublishMessageBusException($"The {bus.Settings.Name} bus has no configured serializer, so it cannot be used with the outbox plugin");

        // Add message to the database, do not call next()
        var outboxMessage = new OutboxMessage
        {
            BusName = bus.Settings.Name,
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
