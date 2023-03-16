namespace SlimMessageBus.Host.Outbox;

using SlimMessageBus.Host.Config;
    
public static class BuilderExtensions
{
    static readonly internal string PropertyOutboxEnabled = "Outbox_Enabled";
    static readonly internal string PropertyTransactionScopeEnabled = "TransactionScope_Enabled";

    /// <summary>
    /// Enables the Publish for all message types in this bus to go via the outbox.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="enabled"></param>
    /// <returns></returns>
    public static MessageBusBuilder UseOutbox(this MessageBusBuilder builder, bool enabled = true)
    {
        builder.Settings.Properties[PropertyOutboxEnabled] = enabled;
        return builder;
    }

    /// <summary>
    /// Enables the Publish for the particular message type to go via the outbox.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="enabled"></param>
    /// <returns></returns>
    public static ProducerBuilder<T> UseOutbox<T>(this ProducerBuilder<T> builder, bool enabled = true)
    {
        builder.Settings.Properties[PropertyOutboxEnabled] = enabled;
        return builder;
    }

    /// <summary>
    /// Enables the cration of <see cref="TransactionScope"/> for every message consumed by this bus.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="enabled"></param>
    /// <returns></returns>
    public static MessageBusBuilder UseTransactionScope(this MessageBusBuilder builder, bool enabled = true)
    {
        builder.Settings.Properties[PropertyTransactionScopeEnabled] = enabled;
        return builder;
    }

    /// <summary>
    /// Enables the cration of <see cref="TransactionScope"/> for every message on this consumer.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="enabled"></param>
    /// <returns></returns>
    public static ConsumerBuilder<T> UseTransactionScope<T>(this ConsumerBuilder<T> builder, bool enabled = true)
    {
        builder.Settings.Properties[PropertyTransactionScopeEnabled] = enabled;
        return builder;
    }

    /// <summary>
    /// Enables the cration of <see cref="TransactionScope"/> every messages on this handler.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="enabled"></param>
    /// <returns></returns>
    public static HandlerBuilder<T, R> UseTransactionScope<T, R>(this HandlerBuilder<T, R> builder, bool enabled = true)
    {
        builder.Settings.Properties[PropertyTransactionScopeEnabled] = enabled;
        return builder;
    }
}
