namespace SlimMessageBus.Host.Outbox.PostgreSql.Configuration;

public static class MessageBusBuilderExtensions
{
    public static MessageBusBuilder AddOutboxUsingPostgreSql(this MessageBusBuilder mbb, Action<PostgreSqlOutboxSettings>? configure = null)
    {
        mbb.PostConfigurationActions.Add(
            services =>
            {
                services.TryAddSingleton(
                    sp =>
                    {
                        var settings = new PostgreSqlOutboxSettings();
                        configure?.Invoke(settings);
                        return settings;
                    });

                services.TryAddSingleton(sp => sp.GetRequiredService<PostgreSqlOutboxSettings>().PostgreSqlSettings);

                services.TryAddSingleton<IPostgreSqlOutboxTemplate, PostgreSqlOutboxTemplate>();
                services.TryAddTransient<OutboxSettings>(sp => sp.GetRequiredService<PostgreSqlOutboxSettings>());

                // Transaction
                var settings = new[] { mbb.Settings }.Concat(mbb.Children.Values.Select(x => x.Settings)).ToList();
                foreach (var consumerMessageType in settings
                    .SelectMany(x => x.Consumers
                        .SelectMany(c => c.Invokers)
                        .Where(ci => ci.ParentSettings.IsEnabledForMessageType(x, BuilderExtensions.PropertyPostgreSqlTransactionEnabled, BuilderExtensions.PropertyPostgreSqlTransactionFilter, ci.MessageType)))
                    .Select(x => x.MessageType))
                {
                    var serviceType = typeof(IConsumerInterceptor<>).MakeGenericType(consumerMessageType);
                    var implementationType = typeof(PostgreSqlTransactionConsumerInterceptor<>).MakeGenericType(consumerMessageType);
                    services.TryAddEnumerable(ServiceDescriptor.Transient(serviceType, implementationType));
                }

                services.TryAddScoped<IPostgreSqlTransactionService, PostgreSqlTransactionService>();

#if !NETSTANDARD2_0
                var factory = ActivatorUtilities.CreateFactory<PostgreSqlOutboxMessageRepository>([typeof(NpgsqlConnection)]);
                services.TryAddScoped(typeof(IPostgreSqlMessageOutboxRepository), sp => factory(sp, [sp.GetRequiredService<NpgsqlConnection>()]));
#else
                services.TryAddScoped(typeof(IPostgreSqlMessageOutboxRepository), sp => ActivatorUtilities.CreateInstance<PostgreSqlOutboxMessageRepository>(sp, sp.GetRequiredService<NpgsqlConnection>()));
#endif

                services.TryAddScoped<IOutboxMessageRepository<PostgreSqlOutboxMessage>>(sp => sp.GetRequiredService<IPostgreSqlMessageOutboxRepository>());
                services.TryAddScoped<IOutboxMessageFactory>(sp => sp.GetRequiredService<IPostgreSqlMessageOutboxRepository>());
                services.TryAddScoped<IPostgreSqlRepository>(sp => sp.GetRequiredService<IPostgreSqlMessageOutboxRepository>());
                services.TryAddTransient<IOutboxMigrationService, PostgreSqlOutboxMigrationService>();
            });

        return mbb.AddOutbox<PostgreSqlOutboxMessage>();
    }
}
