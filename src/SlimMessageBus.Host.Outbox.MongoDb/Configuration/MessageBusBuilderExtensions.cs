namespace SlimMessageBus.Host.Outbox.MongoDb.Configuration;

public static class MessageBusBuilderExtensions
{
    /// <summary>
    /// Configures the outbox to use MongoDB as the backing store.
    /// Requires <see cref="IMongoClient"/> to be registered in the DI container.
    /// </summary>
    public static MessageBusBuilder AddOutboxUsingMongoDb(this MessageBusBuilder mbb, Action<MongoDbOutboxSettings>? configure = null)
    {
        mbb.PostConfigurationActions.Add(services =>
        {
            services.TryAddSingleton(sp =>
            {
                var settings = new MongoDbOutboxSettings();
                configure?.Invoke(settings);
                return settings;
            });

            services.TryAddSingleton(sp => sp.GetRequiredService<MongoDbOutboxSettings>().MongoDbSettings);
            services.TryAddTransient<OutboxSettings>(sp => sp.GetRequiredService<MongoDbOutboxSettings>());

            // MongoDB collections (thread-safe singletons)
            services.TryAddSingleton(sp =>
            {
                var dbSettings = sp.GetRequiredService<MongoDbSettings>();
                var database = sp.GetRequiredService<IMongoDatabase>();
                return database.GetCollection<MongoDbOutboxDocument>(dbSettings.CollectionName);
            });

            services.TryAddSingleton(sp =>
            {
                var dbSettings = sp.GetRequiredService<MongoDbSettings>();
                var database = sp.GetRequiredService<IMongoDatabase>();
                return database.GetCollection<MongoDbOutboxLockDocument>(dbSettings.LockCollectionName);
            });

            // IMongoDatabase resolved from IMongoClient + settings
            services.TryAddSingleton(sp =>
            {
                var dbSettings = sp.GetRequiredService<MongoDbSettings>();
                var client = sp.GetRequiredService<IMongoClient>();
                return client.GetDatabase(dbSettings.DatabaseName);
            });

            // Transaction interceptors
            var settings = new[] { mbb.Settings }.Concat(mbb.Children.Values.Select(x => x.Settings)).ToList();
            foreach (var consumerMessageType in settings
                .SelectMany(x => x.Consumers
                    .SelectMany(c => c.Invokers)
                    .Where(ci => ci.ParentSettings.IsEnabledForMessageType(
                        x,
                        BuilderExtensions.PropertyMongoDbTransactionEnabled,
                        BuilderExtensions.PropertyMongoDbTransactionFilter,
                        ci.MessageType)))
                .Select(x => x.MessageType))
            {
                var serviceType = typeof(IConsumerInterceptor<>).MakeGenericType(consumerMessageType);
                var implementationType = typeof(MongoDbTransactionConsumerInterceptor<>).MakeGenericType(consumerMessageType);
                services.TryAddEnumerable(ServiceDescriptor.Transient(serviceType, implementationType));
            }

            services.TryAddScoped<MongoDbSessionHolder>();
            services.TryAddScoped<IMongoDbTransactionService>(sp => new MongoDbTransactionService(
                sp.GetRequiredService<IMongoClient>(),
                sp.GetRequiredService<MongoDbSessionHolder>()));

            // Expose the active session handle so consumers can inject IClientSessionHandle?
            // directly into their constructor without any dependency on SMB packages.
            // Because SMB now resolves the consumer *after* all interceptors have run,
            // this factory fires after BeginTransaction() has set the session on the holder.
            // Consumers inject IClientSessionHandle? (nullable). The value is null when no
            // transaction is active, and non-null after BeginTransaction() fires (which happens
            // before the consumer is constructed, thanks to the deferred resolution in core SMB).
            services.TryAddScoped<IClientSessionHandle>(sp =>
                sp.GetRequiredService<MongoDbSessionHolder>().Session!);

            services.TryAddScoped<IMongoDbMessageOutboxRepository>(sp => new MongoDbOutboxMessageRepository(
                sp.GetRequiredService<ILogger<MongoDbOutboxMessageRepository>>(),
                sp.GetRequiredService<TimeProvider>(),
                sp.GetRequiredService<IMongoCollection<MongoDbOutboxDocument>>(),
                sp.GetRequiredService<IMongoCollection<MongoDbOutboxLockDocument>>(),
                sp.GetRequiredService<IMongoDbTransactionService>()));
            services.TryAddScoped<IOutboxMessageRepository<MongoDbOutboxMessage>>(sp => sp.GetRequiredService<IMongoDbMessageOutboxRepository>());
            services.TryAddScoped<IOutboxMessageFactory>(sp => sp.GetRequiredService<IMongoDbMessageOutboxRepository>());
            services.TryAddTransient<IOutboxMigrationService, MongoDbOutboxMigrationService>();
        });

        return mbb.AddOutbox<MongoDbOutboxMessage>();
    }
}
