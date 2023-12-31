namespace SlimMessageBus.Host.Outbox.DbContext;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using SlimMessageBus.Host.Outbox.Sql;

public class DbContextOutboxRepository<TDbContext> : SqlOutboxRepository where TDbContext : DbContext
{
    public TDbContext DbContext { get; }

    public DbContextOutboxRepository(ILogger<SqlOutboxRepository> logger, SqlOutboxSettings settings, SqlOutboxTemplate sqlOutboxTemplate, TDbContext dbContext)
        : base(logger, settings, sqlOutboxTemplate, (SqlConnection)dbContext.Database.GetDbConnection())
    {
        DbContext = dbContext;
    }

    public override SqlTransaction CurrentTransaction => (SqlTransaction)DbContext.Database.CurrentTransaction?.GetDbTransaction();

    public override async ValueTask BeginTransaction()
    {
        ValidateNoTransactionStarted();
        await DbContext.Database.BeginTransactionAsync(Settings.SqlSettings.TransactionIsolationLevel);
    }

    public override ValueTask CommitTransaction()
    {
        ValidateTransactionStarted();
        DbContext.Database.CommitTransaction();
        return new ValueTask();
    }

    public override ValueTask RollbackTransaction()
    {
        ValidateTransactionStarted();
        DbContext.Database.RollbackTransaction();
        return new ValueTask();
    }
}
