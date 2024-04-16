namespace SlimMessageBus.Host.Outbox.DbContext;

using Microsoft.Extensions.DependencyInjection.Extensions;

using SlimMessageBus.Host;
using SlimMessageBus.Host.Outbox.Sql;
using SlimMessageBus.Host.Sql.Common;

public static class MessageBusBuilderExtensions
{
    public static MessageBusBuilder AddOutboxUsingDbContext<TDbContext>(this MessageBusBuilder mbb, Action<SqlOutboxSettings> configure)
        where TDbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        mbb.PostConfigurationActions.Add(services =>
        {
            services.TryAddScoped<ISqlTransactionService, DbContextTransactionService<TDbContext>>();
        });
        return mbb.AddOutboxUsingSql<DbContextOutboxRepository<TDbContext>>(configure);
    }
}
