namespace SlimMessageBus.Host.PostgreSql;

public static class PostgreSqlMessageBusBuilderExtensions
{
    public static MessageBusBuilder WithProviderPostgreSql(this MessageBusBuilder mbb, Action<PostgreSqlMessageBusSettings> configure)
    {
        if (mbb == null) throw new ArgumentNullException(nameof(mbb));
        if (configure == null) throw new ArgumentNullException(nameof(configure));

        var providerSettings = new PostgreSqlMessageBusSettings();
        configure(providerSettings);

        mbb.PostConfigurationActions.Add(services =>
        {
            services.TryAddSingleton(providerSettings);
            services.TryAddScoped(_ => new NpgsqlConnection(providerSettings.ConnectionString));
            services.TryAddSingleton<PostgreSqlSequentialGuidGenerator>();
            services.TryAddScoped<PostgreSqlRepository>();
            services.Replace(ServiceDescriptor.Scoped<IPostgreSqlRepository>(svp => svp.GetRequiredService<PostgreSqlRepository>()));
            services.TryAddSingleton<PostgreSqlTemplate>();
        });

        return mbb.WithProvider(settings => new PostgreSqlMessageBus(settings, providerSettings));
    }
}
