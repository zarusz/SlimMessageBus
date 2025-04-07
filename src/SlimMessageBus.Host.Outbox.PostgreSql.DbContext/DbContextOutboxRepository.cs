namespace SlimMessageBus.Host.Outbox.PostgreSql.DbContext;

public class DbContextOutboxRepository<TDbContext> : PostgreSqlOutboxMessageRepository
    where TDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public TDbContext DbContext { get; }

    public DbContextOutboxRepository(
        ILogger<DbContextOutboxRepository<TDbContext>> logger,
        PostgreSqlOutboxSettings settings,
        TimeProvider timeProvider,
        TDbContext dbContext,
        IPostgreSqlOutboxTemplate template,
        IPostgreSqlTransactionService transactionService)
        : base(
            logger,
            settings.PostgreSqlSettings,
            timeProvider,
            template,
            (NpgsqlConnection)dbContext.Database.GetDbConnection(),
            transactionService)
    {
        DbContext = dbContext;
    }
}
