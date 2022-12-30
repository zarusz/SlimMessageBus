namespace SlimMessageBus.Host.Outbox.DbContext;

using Microsoft.Extensions.DependencyInjection;

using SlimMessageBus.Host.Outbox.Sql;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMessageBusOutboxUsingDbContext<TDbContext>(this IServiceCollection services, Action<SqlOutboxSettings> configure)
        where TDbContext : Microsoft.EntityFrameworkCore.DbContext
        => services.AddMessageBusOutboxUsingSql<DbContextOutboxRepository<TDbContext>>(configure);
}
