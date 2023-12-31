namespace SlimMessageBus.Host.Sql;

using Microsoft.Extensions.DependencyInjection;

public static class SqlMessageBusBuilderExtensions
{
    public static MessageBusBuilder WithProviderSql(this MessageBusBuilder mbb, Action<SqlMessageBusSettings> configure)
    {
        if (mbb == null) throw new ArgumentNullException(nameof(mbb));
        if (configure == null) throw new ArgumentNullException(nameof(configure));

        var providerSettings = new SqlMessageBusSettings();
        configure(providerSettings);

        mbb.PostConfigurationActions.Add(services =>
        {
            services.TryAddSingleton(providerSettings);

            services.TryAddScoped<SqlRepository>();
            services.Replace(ServiceDescriptor.Scoped<ISqlRepository>(svp => svp.GetRequiredService<SqlRepository>()));

            /*
            services.Replace(ServiceDescriptor.Transient<OutboxSettings>(svp => svp.GetRequiredService<SqlOutboxSettings>()));

            services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IConsumerInterceptor<>), typeof(SqlTransactionConsumerInterceptor<>)));

            services.TryAddScoped<TOutboxRepository>();
            services.Replace(ServiceDescriptor.Scoped<ISqlOutboxRepository>(svp => svp.GetRequiredService<TOutboxRepository>()));
            services.Replace(ServiceDescriptor.Scoped<IOutboxRepository>(svp => svp.GetRequiredService<TOutboxRepository>()));

            services.TryAddSingleton<SqlTemplate>();
            */
        });

        return mbb.WithProvider(settings => new SqlMessageBus(settings, providerSettings));
    }
}
