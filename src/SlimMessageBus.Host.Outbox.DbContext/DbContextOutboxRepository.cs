namespace SlimMessageBus.Host.Outbox.DbContext;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using SlimMessageBus.Host.Outbox.Sql;
using SlimMessageBus.Host.Sql.Common;

public class DbContextOutboxRepository<TDbContext> : SqlOutboxRepository where TDbContext : DbContext
{
    public TDbContext DbContext { get; }

    public DbContextOutboxRepository(
        ILogger<SqlOutboxRepository> logger,
        SqlOutboxSettings settings,
        SqlOutboxTemplate sqlOutboxTemplate,
        TDbContext dbContext,
        ISqlTransactionService transactionService,
        IOutboxMessageAdapter outboxMessageAdapter)
        : base(
            logger,
            settings,
            sqlOutboxTemplate,
            (SqlConnection)dbContext.Database.GetDbConnection(),
            transactionService,
            outboxMessageAdapter)
    {
        DbContext = dbContext;
    }
}
