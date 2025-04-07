namespace SlimMessageBus.Host.Outbox.PostgreSql.DbContext;

public static class MessageBusBuilderExtensions
{
    public static MessageBusBuilder AddOutboxUsingDbContext<TDbContext>(this MessageBusBuilder mbb, Action<PostgreSqlOutboxSettings> configure)
        where TDbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        mbb.AddOutboxUsingPostgreSql(configure);

        mbb.PostConfigurationActions.Add(
            services =>
            {
                services.Replace(ServiceDescriptor.Describe(typeof(IPostgreSqlTransactionService), typeof(DbContextTransactionService<TDbContext>), ServiceLifetime.Scoped));
                services.Replace(ServiceDescriptor.Describe(typeof(IPostgreSqlMessageOutboxRepository), typeof(DbContextOutboxRepository<TDbContext>), ServiceLifetime.Scoped));
            });

        return mbb;
    }
}
