namespace SlimMessageBus.Host.Outbox.Sql;

using SlimMessageBus.Host;

public static class BuilderExtensions
{
    static readonly internal string PropertySqlTransactionEnabled = "SqlTransaction_Enabled";
    static readonly internal string PropertySqlTransactionFilter = "SqlTransaction_Filter";

    /// <summary>
    /// Enables the creation of <see cref="SqlTransaction"/> for every message consumed by this bus.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="enabled"></param>
    /// <param name="messageTypeFilter">When enabled, allows to decide if the transaction should be created.</param>
    /// <returns></returns>
    public static MessageBusBuilder UseSqlTransaction(this MessageBusBuilder builder, bool enabled = true, Func<Type, bool> messageTypeFilter = null)
    {
        SetTransactionScopeProps(builder.Settings, enabled, messageTypeFilter);
        return builder;
    }

    /// <summary>
    /// Enables the creation of <see cref="SqlTransaction"/> for every message on this consumer.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="enabled"></param>
    /// <param name="messageTypeFilter">When enabled, allows to decide if the transaction should be created.</param>
    /// <returns></returns>
    public static ConsumerBuilder<T> UseSqlTransaction<T>(this ConsumerBuilder<T> builder, bool enabled = true, Func<Type, bool> messageTypeFilter = null)
    {
        SetTransactionScopeProps(builder.Settings, enabled, messageTypeFilter);
        return builder;
    }

    /// <summary>
    /// Enables the creation of <see cref="SqlTransaction"/> every messages on this handler.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="enabled"></param>
    /// <param name="messageTypeFilter">When enabled, allows to decide if the transaction should be created.</param>
    /// <returns></returns>
    public static HandlerBuilder<T, R> UseSqlTransaction<T, R>(this HandlerBuilder<T, R> builder, bool enabled = true, Func<Type, bool> messageTypeFilter = null)
    {
        SetTransactionScopeProps(builder.Settings, enabled, messageTypeFilter);
        return builder;
    }

    private static void SetTransactionScopeProps(HasProviderExtensions hasProviderExtensions, bool enabled, Func<Type, bool> messageTypeFilter = null)
    {
        hasProviderExtensions.Properties[PropertySqlTransactionEnabled] = enabled;
        hasProviderExtensions.Properties[PropertySqlTransactionFilter] = messageTypeFilter;
    }
}
