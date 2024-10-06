namespace SlimMessageBus.Host.Outbox.DbContext;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using SlimMessageBus.Host.Outbox.Sql;
using SlimMessageBus.Host.Sql.Common;

public class DbContextOutboxRepository<TDbContext> : SqlOutboxMessageRepository where TDbContext : DbContext
{
    public TDbContext DbContext { get; }

    public DbContextOutboxRepository(
        ILogger<DbContextOutboxRepository<TDbContext>> logger,
        SqlOutboxSettings settings,
        SqlOutboxTemplate sqlOutboxTemplate,
        IGuidGenerator guidGenerator,
        ICurrentTimeProvider currentTimeProvider,
        IInstanceIdProvider instanceIdProvider,
        TDbContext dbContext,
        ISqlTransactionService transactionService)
        : base(
            logger,
            settings,
            sqlOutboxTemplate,
            guidGenerator,
            currentTimeProvider,
            instanceIdProvider,
            (SqlConnection)dbContext.Database.GetDbConnection(),
            transactionService)
    {
        DbContext = dbContext;
    }
}
