namespace SlimMessageBus.Host.Outbox.Sql;

using SlimMessageBus.Host;

public static class MessageBusBuilderExtensions
{
    public static MessageBusBuilder AddOutboxUsingSql<TOutboxRepository>(this MessageBusBuilder mbb, Action<SqlOutboxSettings> configure)
        where TOutboxRepository : class, ISqlOutboxRepository
    {
        mbb.AddOutbox();

        mbb.PostConfigurationActions.Add(services =>
        {
            services.TryAddSingleton(svp =>
            {
                var settings = new SqlOutboxSettings();
                configure?.Invoke(settings);
                return settings;
            });
            services.Replace(ServiceDescriptor.Transient<OutboxSettings>(svp => svp.GetRequiredService<SqlOutboxSettings>()));

            services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IConsumerInterceptor<>), typeof(SqlTransactionConsumerInterceptor<>)));

            services.TryAddScoped<TOutboxRepository>();
            services.Replace(ServiceDescriptor.Scoped<ISqlOutboxRepository>(svp => svp.GetRequiredService<TOutboxRepository>()));
            services.Replace(ServiceDescriptor.Scoped<IOutboxRepository>(svp => svp.GetRequiredService<TOutboxRepository>()));

            services.TryAddSingleton<SqlOutboxTemplate>();
        });
        return mbb;
    }

    public static MessageBusBuilder AddOutboxUsingSql(this MessageBusBuilder mbb, Action<SqlOutboxSettings> configure)
        => mbb.AddOutboxUsingSql<SqlOutboxRepository>(configure);
}
