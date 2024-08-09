namespace SlimMessageBus.Host.Outbox;

using SlimMessageBus.Host;

public static class BuilderExtensions
{
    static readonly internal string PropertyOutboxEnabled = "Outbox_Enabled";
    static readonly internal string PropertyOutboxFilter = "Outbox_Filter";
    static readonly internal string PropertyTransactionScopeEnabled = "TransactionScope_Enabled";
    static readonly internal string PropertyTransactionScopeFilter = "TransactionScope_Filter";

    /// <summary>
    /// Enables the ProducePublish for all message types in this bus to go via the outbox.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="enabled"></param>
    /// <param name="messageTypeFilter">When enabled, allows to decide if the outbox should be used for the given message type.</param>
    /// <returns></returns>
    public static MessageBusBuilder UseOutbox(this MessageBusBuilder builder, bool enabled = true, Func<Type, bool> messageTypeFilter = null)
    {
        builder.Settings.Properties[PropertyOutboxEnabled] = enabled;
        builder.Settings.Properties[PropertyOutboxFilter] = messageTypeFilter;
        return builder;
    }

    /// <summary>
    /// Enables the ProducePublish for the particular message type to go via the outbox.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="enabled"></param>
    /// <param name="messageTypeFilter">When enabled, allows to decide if the outbox should be used for the given message type.</param>
    /// <returns></returns>
    public static ProducerBuilder<T> UseOutbox<T>(this ProducerBuilder<T> builder, bool enabled = true, Func<Type, bool> messageTypeFilter = null)
    {
        builder.Settings.Properties[PropertyOutboxEnabled] = enabled;
        builder.Settings.Properties[PropertyOutboxFilter] = messageTypeFilter;
        return builder;
    }

    /// <summary>
    /// Enables the creation of <see cref="TransactionScope"/> for every message consumed by this bus.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="enabled"></param>
    /// <param name="messageTypeFilter">When enabled, allows to decide if the transaction should be created.</param>
    /// <returns></returns>
    public static MessageBusBuilder UseTransactionScope(this MessageBusBuilder builder, bool enabled = true, Func<Type, bool> messageTypeFilter = null)
    {
        SetTransactionScopeProps(builder.Settings, enabled, messageTypeFilter);
        return builder;
    }

    /// <summary>
    /// Enables the creation of <see cref="TransactionScope"/> for every message on this consumer.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="enabled"></param>
    /// <param name="messageTypeFilter">When enabled, allows to decide if the transaction should be created.</param>
    /// <returns></returns>
    public static ConsumerBuilder<T> UseTransactionScope<T>(this ConsumerBuilder<T> builder, bool enabled = true, Func<Type, bool> messageTypeFilter = null)
    {
        SetTransactionScopeProps(builder.Settings, enabled, messageTypeFilter);
        return builder;
    }

    /// <summary>
    /// Enables the creation of <see cref="TransactionScope"/> for every messages on this handler.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="enabled"></param>
    /// <param name="messageTypeFilter">When enabled, allows to decide if the transaction should be created.</param>
    /// <returns></returns>
    public static HandlerBuilder<T, R> UseTransactionScope<T, R>(this HandlerBuilder<T, R> builder, bool enabled = true, Func<Type, bool> messageTypeFilter = null)
    {
        SetTransactionScopeProps(builder.Settings, enabled, messageTypeFilter);
        return builder;
    }

    private static void SetTransactionScopeProps(HasProviderExtensions hasProviderExtensions, bool enabled, Func<Type, bool> messageTypeFilter = null)
    {
        hasProviderExtensions.Properties[PropertyTransactionScopeEnabled] = enabled;
        hasProviderExtensions.Properties[PropertyTransactionScopeFilter] = messageTypeFilter;
    }
}
