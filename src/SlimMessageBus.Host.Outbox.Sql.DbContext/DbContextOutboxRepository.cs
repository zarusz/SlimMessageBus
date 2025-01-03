namespace SlimMessageBus.Host.Outbox.Sql.DbContext;

public class DbContextOutboxRepository<TDbContext> : SqlOutboxMessageRepository
    where TDbContext : Microsoft.EntityFrameworkCore.DbContext
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
