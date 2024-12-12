namespace SlimMessageBus.Host.Outbox.Sql.DbContext;

public static class MessageBusBuilderExtensions
{
    public static MessageBusBuilder AddOutboxUsingDbContext<TDbContext>(this MessageBusBuilder mbb, Action<SqlOutboxSettings> configure)
        where TDbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        mbb.PostConfigurationActions.Add(services =>
        {
            services.TryAddScoped<ISqlTransactionService, DbContextTransactionService<TDbContext>>();
            services.TryAddScoped(svp =>
            {
                var settings = svp.GetRequiredService<SqlOutboxSettings>();
                return new DbContextOutboxRepository<TDbContext>(
                        svp.GetRequiredService<ILogger<DbContextOutboxRepository<TDbContext>>>(),
                        settings,
                        svp.GetRequiredService<SqlOutboxTemplate>(),
                        settings.IdGeneration.GuidGenerator ?? (IGuidGenerator)svp.GetRequiredService(settings.IdGeneration.GuidGeneratorType),
                        svp.GetRequiredService<ICurrentTimeProvider>(),
                        svp.GetRequiredService<IInstanceIdProvider>(),
                        svp.GetRequiredService<TDbContext>(),
                        svp.GetRequiredService<ISqlTransactionService>()
                    );
            });
        });
        return mbb.AddOutboxUsingSql<DbContextOutboxRepository<TDbContext>>(configure);
    }
}
