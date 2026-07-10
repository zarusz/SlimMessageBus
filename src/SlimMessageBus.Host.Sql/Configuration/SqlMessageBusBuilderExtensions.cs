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
            services.TryAddSingleton<ISqlSettings>(providerSettings);

            services.TryAddScoped(_ => new SqlConnection(providerSettings.ConnectionString));
            services.TryAddScoped<ISqlTransactionService, SqlTransactionService>();
            services.TryAddSingleton<SqlSequentialGuidGenerator>();
            services.TryAddScoped<SqlRepository>();
            services.Replace(ServiceDescriptor.Scoped<ISqlRepository>(svp => svp.GetRequiredService<SqlRepository>()));
            services.TryAddSingleton<SqlTemplate>();
        });

        return mbb.WithProvider(settings => new SqlMessageBus(settings, providerSettings));
    }
}
