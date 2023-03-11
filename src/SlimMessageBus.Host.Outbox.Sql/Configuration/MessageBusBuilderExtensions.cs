namespace SlimMessageBus.Host.Outbox.Sql;

public static class MessageBusBuilderExtensions
{
    public static MessageBusBuilder AddOutboxUsingSql<TOutboxRepository>(this MessageBusBuilder mbb, Action<SqlOutboxSettings> configure)
        where TOutboxRepository : class, ISqlOutboxRepository
    {
        if (mbb.Services is null)
        {
            return mbb;
        }

        mbb.AddOutbox();
        
        mbb.Services.TryAddSingleton(svp =>
        {
            var settings = new SqlOutboxSettings();
            configure?.Invoke(settings);
            return settings;
        });
        mbb.Services.Replace(ServiceDescriptor.Transient<OutboxSettings>(svp => svp.GetRequiredService<SqlOutboxSettings>()));

        mbb.Services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IConsumerInterceptor<>), typeof(SqlTransactionConsumerInterceptor<>)));

        mbb.Services.TryAddScoped<TOutboxRepository>();
        mbb.Services.Replace(ServiceDescriptor.Scoped<ISqlOutboxRepository>(svp => svp.GetRequiredService<TOutboxRepository>()));
        mbb.Services.Replace(ServiceDescriptor.Scoped<IOutboxRepository>(svp => svp.GetRequiredService<TOutboxRepository>()));

        mbb.Services.TryAddSingleton<SqlOutboxTemplate>();

        return mbb;
    }

    public static MessageBusBuilder AddOutboxUsingSql(this MessageBusBuilder mbb, Action<SqlOutboxSettings> configure)
        => mbb.AddOutboxUsingSql<SqlOutboxRepository>(configure);
}
