namespace SlimMessageBus.Host.Outbox.DbContext;

using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.Outbox.Sql;

public static class MessageBusBuilderExtensions
{
    public static MessageBusBuilder AddOutboxUsingDbContext<TDbContext>(this MessageBusBuilder mbb, Action<SqlOutboxSettings> configure)
        where TDbContext : Microsoft.EntityFrameworkCore.DbContext
        => mbb.AddOutboxUsingSql<DbContextOutboxRepository<TDbContext>>(configure);
}
