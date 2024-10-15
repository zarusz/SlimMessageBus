namespace SlimMessageBus.Host.Outbox.DbContext;

public static class BuilderExtensions
{
    static readonly internal string PropertyDbContextTransactionEnabled = "DbContextTransaction_Enabled";
    static readonly internal string PropertyDbContextTransactionFilter = "DbContextTransaction_Filter";

    /// <summary>
    /// Enables the creation of <see cref="System.Data.IDbTransaction"/> for every message consumed by this bus.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="enabled"></param>
    /// <param name="messageTypeFilter">When enabled, allows to decide if the transaction should be created.</param>
    /// <returns></returns>
    public static MessageBusBuilder UseDbContextTransaction(this MessageBusBuilder builder, bool enabled = true, Func<Type, bool> messageTypeFilter = null)
    {
        SetTransactionProps(builder.Settings, enabled, messageTypeFilter);
        return builder;
    }

    /// <summary>
    /// Enables the creation of <see cref="System.Data.IDbTransaction"/> for every message on this consumer.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="enabled"></param>
    /// <param name="messageTypeFilter">When enabled, allows to decide if the transaction should be created.</param>
    /// <returns></returns>
    public static ConsumerBuilder<T> UseDbContextTransaction<T>(this ConsumerBuilder<T> builder, bool enabled = true, Func<Type, bool> messageTypeFilter = null)
    {
        SetTransactionProps(builder.Settings, enabled, messageTypeFilter);
        return builder;
    }

    /// <summary>
    /// Enables the creation of <see cref="System.Data.IDbTransaction"/> every messages on this handler.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="enabled"></param>
    /// <param name="messageTypeFilter">When enabled, allows to decide if the transaction should be created.</param>
    /// <returns></returns>
    public static HandlerBuilder<T, R> UseDbContextTransaction<T, R>(this HandlerBuilder<T, R> builder, bool enabled = true, Func<Type, bool> messageTypeFilter = null)
    {
        SetTransactionProps(builder.Settings, enabled, messageTypeFilter);
        return builder;
    }

    private static void SetTransactionProps(HasProviderExtensions hasProviderExtensions, bool enabled, Func<Type, bool> messageTypeFilter = null)
    {
        hasProviderExtensions.Properties[PropertyDbContextTransactionEnabled] = enabled;
        hasProviderExtensions.Properties[PropertyDbContextTransactionFilter] = messageTypeFilter;
    }
}
