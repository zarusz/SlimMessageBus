namespace SlimMessageBus.Host.Outbox.Sql;

using SlimMessageBus.Host;

public static class BuilderExtensions
{
    static readonly internal string PropertySqlTransactionEnabled = "SqlTransaction_Enabled";
   
    /// <summary>
    /// Enables the cration of <see cref="SqlTransaction"/> for every message consumed by this bus.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="enabled"></param>
    /// <returns></returns>
    public static MessageBusBuilder UseSqlTransaction(this MessageBusBuilder builder, bool enabled = true)
    {
        builder.Settings.Properties[PropertySqlTransactionEnabled] = enabled;
        return builder;
    }

    /// <summary>
    /// Enables the cration of <see cref="SqlTransaction"/> for every message on this consumer.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="enabled"></param>
    /// <returns></returns>
    public static ConsumerBuilder<T> UseSqlTransaction<T>(this ConsumerBuilder<T> builder, bool enabled = true)
    {
        builder.Settings.Properties[PropertySqlTransactionEnabled] = enabled;
        return builder;
    }

    /// <summary>
    /// Enables the cration of <see cref="SqlTransaction"/> every messages on this handler.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="enabled"></param>
    /// <returns></returns>
    public static HandlerBuilder<T, R> UseSqlTransaction<T, R>(this HandlerBuilder<T, R> builder, bool enabled = true)
    {
        builder.Settings.Properties[PropertySqlTransactionEnabled] = enabled;
        return builder;
    }
}
