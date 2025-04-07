namespace SlimMessageBus.Host.Outbox.PostgreSql;

public static class BuilderExtensions
{
    static readonly internal string PropertyPostgreSqlTransactionEnabled = "PostgreSqlTransaction_Enabled";
    static readonly internal string PropertyPostgreSqlTransactionFilter = "PostgreSqlTransaction_Filter";

    /// <summary>
    /// Enables the creation of <see cref="SqlTransaction"/> for every message consumed by this bus.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="enabled"></param>
    /// <param name="messageTypeFilter">When enabled, allows to decide if the transaction should be created.</param>
    /// <returns></returns>
    public static MessageBusBuilder UsePostgreSqlTransaction(this MessageBusBuilder builder, bool enabled = true, Func<Type, bool>? messageTypeFilter = null)
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
    public static ConsumerBuilder<T> UsePostgreSqlTransaction<T>(this ConsumerBuilder<T> builder, bool enabled = true, Func<Type, bool>? messageTypeFilter = null)
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
    public static HandlerBuilder<T, R> UsePostgreSqlTransaction<T, R>(this HandlerBuilder<T, R> builder, bool enabled = true, Func<Type, bool>? messageTypeFilter = null)
    {
        SetTransactionScopeProps(builder.Settings, enabled, messageTypeFilter);
        return builder;
    }

    private static void SetTransactionScopeProps(HasProviderExtensions hasProviderExtensions, bool enabled, Func<Type, bool>? messageTypeFilter = null)
    {
        hasProviderExtensions.Properties[PropertyPostgreSqlTransactionEnabled] = enabled;
        hasProviderExtensions.Properties[PropertyPostgreSqlTransactionFilter] = messageTypeFilter;
    }
}
