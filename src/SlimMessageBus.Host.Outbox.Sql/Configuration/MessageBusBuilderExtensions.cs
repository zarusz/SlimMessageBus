namespace SlimMessageBus.Host.Outbox.Sql;

public static class MessageBusBuilderExtensions
{
    public static MessageBusBuilder AddOutboxUsingSql<TOutboxRepository>(this MessageBusBuilder mbb, Action<SqlOutboxSettings> configure)
        where TOutboxRepository : class, ISqlMessageOutboxRepository
    {
        mbb.PostConfigurationActions.Add(services =>
        {
            var settings = new[] { mbb.Settings }.Concat(mbb.Children.Values.Select(x => x.Settings)).ToList();

            services.TryAddSingleton(svp =>
            {
                var settings = new SqlOutboxSettings();
                configure?.Invoke(settings);
                return settings;
            });
            services.TryAddTransient<OutboxSettings>(svp => svp.GetRequiredService<SqlOutboxSettings>());
            services.TryAddSingleton<ISqlSettings>(svp => svp.GetRequiredService<SqlOutboxSettings>().SqlSettings);

            // Optimization: only register generic interceptors in the DI for particular message types that have opted in for transaction scope
            foreach (var consumerMessageType in settings
                .SelectMany(x => x.Consumers
                    .SelectMany(c => c.Invokers)
                    .Where(ci => ci.ParentSettings.IsEnabledForMessageType(x, BuilderExtensions.PropertySqlTransactionEnabled, BuilderExtensions.PropertySqlTransactionFilter, ci.MessageType)))
                .Select(x => x.MessageType))
            {
                var serviceType = typeof(IConsumerInterceptor<>).MakeGenericType(consumerMessageType);
                var implementationType = typeof(SqlTransactionConsumerInterceptor<>).MakeGenericType(consumerMessageType);
                services.TryAddEnumerable(ServiceDescriptor.Transient(serviceType, implementationType));
            }

            services.TryAddScoped<ISqlTransactionService, SqlTransactionService>();
            services.TryAddScoped<ISqlMessageOutboxRepository>(svp =>
            {
                var outboxRepository = svp.GetRequiredService<TOutboxRepository>();
                var settings = svp.GetRequiredService<SqlOutboxSettings>();
                if (settings.MeasureSqlOperations)
                {
                    return new MeasuringSqlMessageOutboxRepositoryDecorator(outboxRepository, svp.GetRequiredService<ILogger<MeasuringSqlMessageOutboxRepositoryDecorator>>());
                }
                return outboxRepository;
            });

            services.TryAddScoped<IOutboxMessageFactory>(svp => svp.GetRequiredService<TOutboxRepository>());
            services.TryAddScoped<IOutboxMessageRepository<SqlOutboxMessage>>(svp => svp.GetRequiredService<ISqlMessageOutboxRepository>());

            services.TryAddSingleton<SqlOutboxTemplate>();
            services.TryAddTransient<IOutboxMigrationService>(svp => new SqlOutboxMigrationService(
                svp.GetRequiredService<ILogger<SqlOutboxMigrationService>>(),
                svp.GetRequiredService<TOutboxRepository>(),
                svp.GetRequiredService<ISqlTransactionService>(),
                svp.GetRequiredService<SqlOutboxSettings>()));
        });
        return mbb.AddOutbox<SqlOutboxMessage>();
    }

    public static MessageBusBuilder AddOutboxUsingSql(this MessageBusBuilder mbb, Action<SqlOutboxSettings> configure)
    {
        mbb.PostConfigurationActions.Add(services =>
        {
            services.TryAddScoped(svp =>
            {
                var settings = svp.GetRequiredService<SqlOutboxSettings>();

                return new SqlOutboxMessageRepository(
                        svp.GetRequiredService<ILogger<SqlOutboxMessageRepository>>(),
                        settings,
                        svp.GetRequiredService<SqlOutboxTemplate>(),
                        settings.IdGeneration.GuidGenerator ?? (IGuidGenerator)svp.GetRequiredService(settings.IdGeneration.GuidGeneratorType),
                        svp.GetRequiredService<TimeProvider>(),
                        svp.GetRequiredService<IInstanceIdProvider>(),
                        svp.GetRequiredService<SqlConnection>(),
                        svp.GetRequiredService<ISqlTransactionService>()
                    );
            });
        });
        return mbb.AddOutboxUsingSql<SqlOutboxMessageRepository>(configure);
    }
}
