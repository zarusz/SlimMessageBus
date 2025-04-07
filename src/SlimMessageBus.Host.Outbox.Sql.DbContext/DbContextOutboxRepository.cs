namespace SlimMessageBus.Host.Outbox.Sql.DbContext;

public class DbContextOutboxRepository<TDbContext> : SqlOutboxMessageRepository
    where TDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public TDbContext DbContext { get; }

    public DbContextOutboxRepository(
        ILogger<DbContextOutboxRepository<TDbContext>> logger,
        SqlOutboxSettings settings,
        ISqlOutboxTemplate sqlOutboxTemplate,
        IGuidGenerator guidGenerator,
        TimeProvider timeProvider,
        IInstanceIdProvider instanceIdProvider,
        TDbContext dbContext,
        ISqlTransactionService transactionService)
        : base(
            logger,
            settings,
            sqlOutboxTemplate,
            guidGenerator,
            timeProvider,
            instanceIdProvider,
            (SqlConnection)dbContext.Database.GetDbConnection(),
            transactionService)
    {
        DbContext = dbContext;
    }
}
