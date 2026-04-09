namespace SlimMessageBus.Host.Outbox.MongoDb.Configuration;

public static class BuilderExtensions
{
    internal static readonly string PropertyMongoDbTransactionEnabled = "MongoDbTransaction_Enabled";
    internal static readonly string PropertyMongoDbTransactionFilter = "MongoDbTransaction_Filter";

    /// <summary>
    /// Enables wrapping of every consumed message on this bus in a MongoDB session transaction.
    /// Requires MongoDB to be running as a replica set.
    /// </summary>
    public static MessageBusBuilder UseMongoDbTransaction(this MessageBusBuilder builder, bool enabled = true, Func<Type, bool>? messageTypeFilter = null)
    {
        SetTransactionProps(builder.Settings, enabled, messageTypeFilter);
        return builder;
    }

    /// <summary>
    /// Enables wrapping of every consumed message on this consumer in a MongoDB session transaction.
    /// Requires MongoDB to be running as a replica set.
    /// </summary>
    public static ConsumerBuilder<T> UseMongoDbTransaction<T>(this ConsumerBuilder<T> builder, bool enabled = true, Func<Type, bool>? messageTypeFilter = null)
    {
        SetTransactionProps(builder.Settings, enabled, messageTypeFilter);
        return builder;
    }

    /// <summary>
    /// Enables wrapping of every request handler on this handler in a MongoDB session transaction.
    /// Requires MongoDB to be running as a replica set.
    /// </summary>
    public static HandlerBuilder<T, R> UseMongoDbTransaction<T, R>(this HandlerBuilder<T, R> builder, bool enabled = true, Func<Type, bool>? messageTypeFilter = null)
    {
        SetTransactionProps(builder.Settings, enabled, messageTypeFilter);
        return builder;
    }

    private static void SetTransactionProps(HasProviderExtensions settings, bool enabled, Func<Type, bool>? messageTypeFilter)
    {
        settings.Properties[PropertyMongoDbTransactionEnabled] = enabled;
        settings.Properties[PropertyMongoDbTransactionFilter] = messageTypeFilter;
    }
}
